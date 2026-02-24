using FFXIV.Application.Gear;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.Gear;

public class CombatGearRecommendationTests
{
    private readonly IEquipmentRepository _equipmentRepo = Substitute.For<IEquipmentRepository>();
    private readonly IItemSourceRepository _itemSourceRepo = Substitute.For<IItemSourceRepository>();
    private readonly GearRecommendationService _service;

    public CombatGearRecommendationTests()
    {
        _itemSourceRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, List<ItemSource>>());

        _service = new GearRecommendationService(_equipmentRepo, _itemSourceRepo);
    }

    [Fact]
    public async Task TankJob_ReturnsTankGear_SortedByStatWeights()
    {
        // Arrange
        var highVitDefense = EquipmentTestDataBuilder.CreateCombatEquipment(
            1, "Titanium Cuirass", EquipSlot.Body,
            vitality: 200, strength: 100, defense: 150, magicDefense: 100,
            criticalHit: 80, determination: 60, physicalDamage: 0);
        var highCritDet = EquipmentTestDataBuilder.CreateCombatEquipment(
            2, "Mythril Cuirass", EquipSlot.Body,
            vitality: 150, strength: 80, defense: 100, magicDefense: 80,
            criticalHit: 120, determination: 100, physicalDamage: 0);

        _equipmentRepo.GetBestBySlotAsync(EquipSlot.Body, "PLD", 100, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { highVitDefense, highCritDet }.AsReadOnly());

        // Act
        var result = await _service.GetSlotRecommendationAsync(
            EquipSlot.Body, "PLD", 100, "TankBalanced");

        // Assert: TankBalanced weights VIT=2.0, STR=1.5, DEF=1.5, MDEF=1.0, CRIT=1.2, DET=1.0
        // highVitDefense: 200*2 + 100*1.5 + 150*1.5 + 100*1.0 + 80*1.2 + 60*1.0 = 400+150+225+100+96+60 = 1031
        // highCritDet:    150*2 + 80*1.5 + 100*1.5 + 80*1.0 + 120*1.2 + 100*1.0 = 300+120+150+80+144+100 = 894
        result.RecommendedItem.Should().NotBeNull();
        result.RecommendedItem!.EquipmentId.Should().Be(1);
        result.WeightedScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HealerJob_MindWeighted_SelectsHighMindGear()
    {
        // Arrange
        var highMind = EquipmentTestDataBuilder.CreateCombatEquipment(
            1, "Astral Cane", EquipSlot.MainHand,
            vitality: 100, mind: 300, criticalHit: 80, determination: 60,
            physicalDamage: 120, classJobCategory: "WHM,SCH,AST,SGE");
        var lowMind = EquipmentTestDataBuilder.CreateCombatEquipment(
            2, "Iron Cane", EquipSlot.MainHand,
            vitality: 80, mind: 200, criticalHit: 120, determination: 100,
            physicalDamage: 100, classJobCategory: "WHM,SCH,AST,SGE");

        _equipmentRepo.GetBestBySlotAsync(EquipSlot.MainHand, "WHM", 100, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { highMind, lowMind }.AsReadOnly());

        // Act
        var result = await _service.GetSlotRecommendationAsync(
            EquipSlot.MainHand, "WHM", 100, "HealerBalanced");

        // Assert: HealerBalanced strongly weights Mind=2.0 and PhysicalDamage=5.0
        result.RecommendedItem.Should().NotBeNull();
        result.RecommendedItem!.EquipmentId.Should().Be(1); // higher Mind + higher weapon DMG
    }

    [Fact]
    public void GetAvailableProfiles_TankJob_ReturnsTankProfiles()
    {
        var profiles = _service.GetAvailableProfiles("PLD");
        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("TankBalanced");
    }

    [Fact]
    public void GetAvailableProfiles_HealerJob_ReturnsHealerProfiles()
    {
        var profiles = _service.GetAvailableProfiles("WHM");
        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("HealerBalanced");
    }

    [Fact]
    public void GetAvailableProfiles_MeleeDPS_ReturnsMeleeDPSProfiles()
    {
        var profiles = _service.GetAvailableProfiles("DRG");
        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("MeleeDPSBalanced");
    }

    [Fact]
    public void GetAvailableProfiles_RangedDPS_ReturnsRangedDPSProfiles()
    {
        var profiles = _service.GetAvailableProfiles("BRD");
        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("RangedDPSBalanced");
    }

    [Fact]
    public void GetAvailableProfiles_MagicDPS_ReturnsMagicDPSProfiles()
    {
        var profiles = _service.GetAvailableProfiles("BLM");
        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("MagicDPSBalanced");
    }

    [Fact]
    public async Task CombatJobWithDoHProfile_FallsBackToCombatProfile()
    {
        // Using "Balanced" (a DoH profile) for a tank job should auto-correct
        var gear = EquipmentTestDataBuilder.CreateCombatEquipment(
            1, "Tank Helm", EquipSlot.Head,
            vitality: 200, strength: 100, defense: 80);

        _equipmentRepo.GetBestBySlotAsync(EquipSlot.Head, "WAR", 100, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { gear }.AsReadOnly());

        // Act: Pass "Balanced" (DoH profile) for WAR (tank) - should auto-correct
        var result = await _service.GetSlotRecommendationAsync(
            EquipSlot.Head, "WAR", 100, "Balanced");

        // Assert: should still score using TankBalanced weights
        result.RecommendedItem.Should().NotBeNull();
        result.WeightedScore.Should().BeGreaterThan(0);
        // With TankBalanced: 200*2 + 100*1.5 + 80*1.5 = 400 + 150 + 120 = 670
        result.WeightedScore.Should().BeApproximately(670, 0.01);
    }

    [Fact]
    public async Task CraftableCombatGear_HasRecipeLink()
    {
        // Arrange
        var craftable = EquipmentTestDataBuilder.CreateCombatEquipment(
            1, "Crafted Sword", EquipSlot.MainHand,
            vitality: 100, strength: 200, physicalDamage: 120,
            classJobCategory: "PLD",
            sourceType: ItemSourceType.Crafted, recipeId: 42);

        _equipmentRepo.GetBestBySlotAsync(EquipSlot.MainHand, "PLD", 100, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { craftable }.AsReadOnly());

        // Act
        var result = await _service.GetSlotRecommendationAsync(
            EquipSlot.MainHand, "PLD", 100, "TankBalanced");

        // Assert
        result.Source.Should().NotBeNull();
        result.Source!.RecipeId.Should().Be(42);
        result.Source.SourceType.Should().Be(ItemSourceType.Crafted);
    }

    [Fact]
    public void CombatStatScoring_WeaponDamageHasHighestWeight()
    {
        // TankBalanced: PhysicalDamageWeight = 5.0 (highest)
        var profile = StatWeightProfiles.TankBalanced;
        profile.PhysicalDamageWeight.Should().Be(5.0);
        profile.VitalityWeight.Should().Be(2.0);
        profile.StrengthWeight.Should().Be(1.5);
    }

    [Fact]
    public void GetProfilesForRole_AllRolesReturnProfiles()
    {
        foreach (var role in Enum.GetValues<CombatRole>())
        {
            var profiles = StatWeightProfiles.GetProfilesForRole(role);
            profiles.Should().NotBeEmpty($"role {role} should have at least one profile");
        }
    }

    [Fact]
    public void JobClassification_AllCombatJobsRecognized()
    {
        var combatJobs = new[] { "PLD", "WAR", "DRK", "GNB", "WHM", "SCH", "AST", "SGE",
            "MNK", "DRG", "NIN", "SAM", "RPR", "VPR", "BRD", "MCH", "DNC", "BLM", "SMN", "RDM", "PCT" };

        foreach (var job in combatJobs)
        {
            JobClassification.IsCombat(job).Should().BeTrue($"{job} should be combat");
            JobClassification.IsKnownJob(job).Should().BeTrue($"{job} should be known");
            JobClassification.GetCombatRole(job).Should().NotBeNull($"{job} should have a role");
        }
    }
}
