using FFXIV.Application.Gear;
using FFXIV.Application.Gear.DTOs;
using FFXIV.Application.Gear.Queries;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.Gear;

public class GearBatchLoadingTests
{
    private readonly IEquipmentRepository _equipmentRepo = Substitute.For<IEquipmentRepository>();
    private readonly IItemSourceRepository _itemSourceRepo = Substitute.For<IItemSourceRepository>();
    private readonly GearRecommendationService _service;

    public GearBatchLoadingTests()
    {
        _itemSourceRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, List<ItemSource>>());

        _service = new GearRecommendationService(_equipmentRepo, _itemSourceRepo);
    }

    [Fact]
    public async Task CalculateSetStatsAsync_UsesBatchLoad_NotIndividualCalls()
    {
        // Arrange
        var head = EquipmentTestDataBuilder.CreateDoHEquipment(1, "Head", EquipSlot.Head, craftsmanship: 50, control: 30, cp: 5);
        var body = EquipmentTestDataBuilder.CreateDoHEquipment(2, "Body", EquipSlot.Body, craftsmanship: 80, control: 50, cp: 8);
        var hands = EquipmentTestDataBuilder.CreateDoHEquipment(3, "Hands", EquipSlot.Hands, craftsmanship: 40, control: 25, cp: 4);

        _equipmentRepo.GetByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { head, body, hands }.AsReadOnly());

        var equipmentIds = new Dictionary<EquipSlot, int>
        {
            [EquipSlot.Head] = 1,
            [EquipSlot.Body] = 2,
            [EquipSlot.Hands] = 3,
        };

        // Act
        var stats = await _service.CalculateSetStatsAsync(equipmentIds);

        // Assert — batch method called once, individual GetByIdAsync never called
        await _equipmentRepo.Received(1).GetByIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>());
        await _equipmentRepo.DidNotReceive().GetByIdAsync(
            Arg.Any<int>(), Arg.Any<CancellationToken>());

        stats.Craftsmanship.Should().Be(170);
        stats.Control.Should().Be(105);
        stats.CP.Should().Be(17);
    }

    [Fact]
    public async Task GetRecommendationsAsync_UsesBatchSlotLoad()
    {
        // Arrange
        var mainHand = EquipmentTestDataBuilder.CreateDoHEquipment(
            10, "Hammer", EquipSlot.MainHand, craftsmanship: 100, control: 60, cp: 10,
            classJobCategory: "BSM");
        var head = EquipmentTestDataBuilder.CreateDoHEquipment(
            11, "Hat", EquipSlot.Head, craftsmanship: 50, control: 30, cp: 5);
        var ring = EquipmentTestDataBuilder.CreateDoHEquipment(
            12, "Ring", EquipSlot.Ring, craftsmanship: 20, control: 15, cp: 3);

        _equipmentRepo.GetBestForSlotsAsync(
            Arg.Any<IEnumerable<EquipSlot>>(), "BSM", 90, 10, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { mainHand, head, ring }.AsReadOnly());

        // Act
        var result = await _service.GetRecommendationsAsync("BSM", 90, "balanced");

        // Assert — batch method called once, individual GetBestBySlotAsync never called
        await _equipmentRepo.Received(1).GetBestForSlotsAsync(
            Arg.Any<IEnumerable<EquipSlot>>(), "BSM", 90, 10, Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _equipmentRepo.DidNotReceive().GetBestBySlotAsync(
            Arg.Any<EquipSlot>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

        result.Slots.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetGearComparisonQueryHandler_UsesBatchLoad()
    {
        // Arrange
        var mediator = Substitute.For<MediatR.IMediator>();
        var profileRepo = Substitute.For<ICharacterProfileRepository>();
        var equipmentRepo = Substitute.For<IEquipmentRepository>();

        var gearSetDto = new GearSetDto(
            Job: "CRP",
            JobName: "Carpenter",
            Level: 90,
            Profile: new StatWeightProfileDto("Balanced", "Balanced", "Equal weight on all stats"),
            Slots: [],
            TotalStats: new GearStatsDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        mediator.Send(Arg.Any<GetGearRecommendationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(gearSetDto);

        var profile = new CharacterProfile
        {
            Id = 1,
            UserId = "user1",
            CharacterName = "Test",
            ServerName = "Excalibur",
            ActiveJob = "CRP",
            ActiveJobLevel = 90,
            CurrentGear = new Dictionary<string, int>
            {
                ["MainHand"] = 100,
                ["Head"] = 101,
                ["Body"] = 102,
            }
        };

        profileRepo.GetByUserIdAsync("user1", Arg.Any<CancellationToken>())
            .Returns(profile);

        var eq100 = EquipmentTestDataBuilder.CreateDoHEquipment(100, "MH", EquipSlot.MainHand, craftsmanship: 80);
        var eq101 = EquipmentTestDataBuilder.CreateDoHEquipment(101, "Head", EquipSlot.Head, craftsmanship: 40);
        var eq102 = EquipmentTestDataBuilder.CreateDoHEquipment(102, "Body", EquipSlot.Body, craftsmanship: 60);

        equipmentRepo.GetByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { eq100, eq101, eq102 }.AsReadOnly());

        var handler = new GetGearComparisonQueryHandler(mediator, profileRepo, equipmentRepo);
        var query = new GetGearComparisonQuery("user1", "CRP", 90, "balanced");

        // Act
        await handler.Handle(query, CancellationToken.None);

        // Assert — batch method called once, individual never called
        await equipmentRepo.Received(1).GetByIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>());
        await equipmentRepo.DidNotReceive().GetByIdAsync(
            Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
