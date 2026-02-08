using FFXIV.Application.Gear;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.Gear;

public class GearRecommendationServiceTests
{
    private readonly IEquipmentRepository _equipmentRepo = Substitute.For<IEquipmentRepository>();
    private readonly IItemSourceRepository _itemSourceRepo = Substitute.For<IItemSourceRepository>();
    private readonly GearRecommendationService _service;

    public GearRecommendationServiceTests()
    {
        _itemSourceRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, List<ItemSource>>());

        _service = new GearRecommendationService(_equipmentRepo, _itemSourceRepo);
    }

    [Fact]
    public async Task ScoreCalculation_ProgressFocused_PrefersHighCraftsmanship()
    {
        // Arrange
        var highCraft = EquipmentTestDataBuilder.CreateDoHEquipment(
            1, "High Craft Hat", EquipSlot.Head, craftsmanship: 100, control: 50, cp: 10);
        var highControl = EquipmentTestDataBuilder.CreateDoHEquipment(
            2, "High Control Hat", EquipSlot.Head, craftsmanship: 50, control: 100, cp: 10);

        _equipmentRepo.GetBestBySlotAsync(EquipSlot.Head, "BSM", 90, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { highCraft, highControl }.AsReadOnly());

        // Act
        var result = await _service.GetSlotRecommendationAsync(
            EquipSlot.Head, "BSM", 90, "ProgressFocused");

        // Assert: highCraft wins (100*1.5 + 50*0.7 + 10*1.0 = 150+35+10 = 195)
        //         highControl scores (50*1.5 + 100*0.7 + 10*1.0 = 75+70+10 = 155)
        result.RecommendedItem.Should().NotBeNull();
        result.RecommendedItem!.EquipmentId.Should().Be(1);
        result.WeightedScore.Should().BeApproximately(195, 0.01);
        result.Alternatives.Should().HaveCount(1);
        result.Alternatives[0].EquipmentId.Should().Be(2);
    }

    [Fact]
    public async Task ScoreCalculation_QualityFocused_PrefersHighControl()
    {
        // Arrange
        var highCraft = EquipmentTestDataBuilder.CreateDoHEquipment(
            1, "High Craft Hat", EquipSlot.Head, craftsmanship: 100, control: 50, cp: 10);
        var highControl = EquipmentTestDataBuilder.CreateDoHEquipment(
            2, "High Control Hat", EquipSlot.Head, craftsmanship: 50, control: 100, cp: 10);

        _equipmentRepo.GetBestBySlotAsync(EquipSlot.Head, "BSM", 90, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { highCraft, highControl }.AsReadOnly());

        // Act
        var result = await _service.GetSlotRecommendationAsync(
            EquipSlot.Head, "BSM", 90, "QualityFocused");

        // Assert: highControl wins (50*0.7 + 100*1.5 + 10*1.0 = 35+150+10 = 195)
        result.RecommendedItem!.EquipmentId.Should().Be(2);
    }

    [Fact]
    public async Task RingSlots_RecommendDifferentItems()
    {
        // Arrange
        var bestRing = EquipmentTestDataBuilder.CreateDoHEquipment(
            10, "Best Ring", EquipSlot.Ring, craftsmanship: 80, control: 80, cp: 5);
        var secondRing = EquipmentTestDataBuilder.CreateDoHEquipment(
            11, "Second Ring", EquipSlot.Ring, craftsmanship: 75, control: 75, cp: 5);

        _equipmentRepo.GetBestBySlotAsync(EquipSlot.Ring, "BSM", 90, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { bestRing, secondRing }.AsReadOnly());
        SetupEmptySlots("BSM", 90, EquipSlot.Ring);

        // Act
        var result = await _service.GetRecommendationsAsync("BSM", 90);

        // Assert
        var ringSlots = result.Slots.Where(s => s.Slot == EquipSlot.Ring).ToList();
        ringSlots.Should().HaveCount(2);
        ringSlots[0].RecommendedItem!.EquipmentId.Should().Be(10);
        ringSlots[1].RecommendedItem!.EquipmentId.Should().Be(11);
        ringSlots[0].SlotLabel.Should().Be("Ring 1");
        ringSlots[1].SlotLabel.Should().Be("Ring 2");
    }

    [Fact]
    public void GetAvailableProfiles_DoH_ReturnsCraftingProfiles()
    {
        var profiles = _service.GetAvailableProfiles("BSM");

        profiles.Should().HaveCount(4);
        profiles.Should().Contain(p => p.Name == "Balanced");
        profiles.Should().Contain(p => p.Name == "ProgressFocused");
        profiles.Should().Contain(p => p.Name == "QualityFocused");
        profiles.Should().Contain(p => p.Name == "CpFocused");
    }

    [Fact]
    public void GetAvailableProfiles_DoL_ReturnsGatheringProfiles()
    {
        var profiles = _service.GetAvailableProfiles("MIN");

        profiles.Should().HaveCount(2);
        profiles.Should().Contain(p => p.Name == "GatheringBalanced");
        profiles.Should().Contain(p => p.Name == "PerceptionFocused");
    }

    [Fact]
    public async Task DoLJob_WithDoHProfile_FallsBackToGatheringBalanced()
    {
        // Arrange
        var pickaxe = EquipmentTestDataBuilder.CreateDoLEquipment(
            20, "Miner Pickaxe", EquipSlot.MainHand, gathering: 100, perception: 50, gp: 10);

        _equipmentRepo.GetBestBySlotAsync(Arg.Any<EquipSlot>(), "MIN", 90, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { pickaxe }.AsReadOnly());

        // Act — request "Balanced" (a DoH profile) for a DoL job
        var result = await _service.GetRecommendationsAsync("MIN", 90, "Balanced");

        // Assert — should auto-correct to GatheringBalanced
        result.Profile.Name.Should().Be("GatheringBalanced");
    }

    [Fact]
    public async Task EmptySlots_WhenNoEquipmentFound_SetsNullRecommendation()
    {
        // Arrange
        _equipmentRepo.GetBestBySlotAsync(Arg.Any<EquipSlot>(), "BSM", 1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment>().AsReadOnly());

        // Act
        var result = await _service.GetRecommendationsAsync("BSM", 1);

        // Assert
        result.Slots.Should().NotBeEmpty();
        result.Slots.Should().OnlyContain(s => s.RecommendedItem == null);
        result.CompletionPercentage.Should().Be(0);
    }

    [Fact]
    public async Task StatAggregation_SumsAcrossAllSlots()
    {
        // Arrange
        var head = EquipmentTestDataBuilder.CreateDoHEquipment(
            1, "Hat", EquipSlot.Head, craftsmanship: 50, control: 30, cp: 5);
        var body = EquipmentTestDataBuilder.CreateDoHEquipment(
            2, "Coat", EquipSlot.Body, craftsmanship: 60, control: 40, cp: 8);

        _equipmentRepo.GetBestBySlotAsync(EquipSlot.Head, "BSM", 90, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { head }.AsReadOnly());
        _equipmentRepo.GetBestBySlotAsync(EquipSlot.Body, "BSM", 90, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { body }.AsReadOnly());
        SetupEmptySlots("BSM", 90, EquipSlot.Head, EquipSlot.Body);

        // Act
        var result = await _service.GetRecommendationsAsync("BSM", 90);

        // Assert
        result.TotalStats.Craftsmanship.Should().Be(110);
        result.TotalStats.Control.Should().Be(70);
        result.TotalStats.CP.Should().Be(13);
    }

    [Fact]
    public async Task CUL_DoesNotIncludeOffHandSlot()
    {
        // Arrange
        _equipmentRepo.GetBestBySlotAsync(Arg.Any<EquipSlot>(), "CUL", 90, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment>().AsReadOnly());

        // Act
        var result = await _service.GetRecommendationsAsync("CUL", 90);

        // Assert — CUL should have 11 slots (no off hand)
        result.Slots.Should().NotContain(s => s.SlotLabel == "Off Hand");
        result.Slots.Should().HaveCount(11);
    }

    [Fact]
    public async Task BSM_IncludesOffHandSlot()
    {
        // Arrange
        _equipmentRepo.GetBestBySlotAsync(Arg.Any<EquipSlot>(), "BSM", 90, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment>().AsReadOnly());

        // Act
        var result = await _service.GetRecommendationsAsync("BSM", 90);

        // Assert — BSM should have 12 slots (includes off hand)
        result.Slots.Should().Contain(s => s.SlotLabel == "Off Hand");
        result.Slots.Should().HaveCount(12);
    }

    [Fact]
    public async Task CompletionPercentage_ReflectsFilledSlots()
    {
        // Arrange — BSM has 12 slots, only Head has equipment
        var head = EquipmentTestDataBuilder.CreateDoHEquipment(
            1, "Hat", EquipSlot.Head, craftsmanship: 50);

        _equipmentRepo.GetBestBySlotAsync(EquipSlot.Head, "BSM", 90, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { head }.AsReadOnly());
        SetupEmptySlots("BSM", 90, EquipSlot.Head);

        // Act
        var result = await _service.GetRecommendationsAsync("BSM", 90);

        // Assert — 1 filled out of 12 total slots
        result.CompletionPercentage.Should().BeApproximately(100.0 / 12.0, 0.1);
    }

    [Fact]
    public async Task CalculateSetStats_SumsEquipmentStats()
    {
        // Arrange
        var head = EquipmentTestDataBuilder.CreateDoHEquipment(
            1, "Hat", EquipSlot.Head, craftsmanship: 50, control: 30, cp: 5);
        var body = EquipmentTestDataBuilder.CreateDoHEquipment(
            2, "Coat", EquipSlot.Body, craftsmanship: 60, control: 40, cp: 8);

        _equipmentRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(head);
        _equipmentRepo.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(body);

        var equipmentIds = new Dictionary<EquipSlot, int>
        {
            [EquipSlot.Head] = 1,
            [EquipSlot.Body] = 2
        };

        // Act
        var stats = await _service.CalculateSetStatsAsync(equipmentIds);

        // Assert
        stats.Craftsmanship.Should().Be(110);
        stats.Control.Should().Be(70);
        stats.CP.Should().Be(13);
    }

    [Fact]
    public async Task SourceInfo_CraftedEquipment_IncludesRecipeId()
    {
        // Arrange
        var craftedBody = EquipmentTestDataBuilder.CreateDoHEquipment(
            1, "Crafted Coat", EquipSlot.Body,
            craftsmanship: 60, control: 40, cp: 8,
            sourceType: ItemSourceType.Crafted, recipeId: 42);

        _equipmentRepo.GetBestBySlotAsync(EquipSlot.Body, "BSM", 90, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { craftedBody }.AsReadOnly());

        // Act
        var result = await _service.GetSlotRecommendationAsync(EquipSlot.Body, "BSM", 90);

        // Assert
        result.Source.SourceType.Should().Be(ItemSourceType.Crafted);
        result.Source.RecipeId.Should().Be(42);
        result.Source.Description.Should().Contain("Recipe #42");
    }

    private void SetupEmptySlots(string job, int level, params EquipSlot[] except)
    {
        var exceptSet = new HashSet<EquipSlot>(except);

        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            if (exceptSet.Contains(slot)) continue;
            _equipmentRepo.GetBestBySlotAsync(slot, job, level, Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new List<Equipment>().AsReadOnly());
        }
    }
}
