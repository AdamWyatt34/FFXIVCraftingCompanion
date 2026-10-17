using FFXIV.Application.Profile.Queries;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.Profile;

public class GetProfileQueryHandlerTests
{
    private readonly ICharacterProfileRepository _profiles = Substitute.For<ICharacterProfileRepository>();
    private readonly IEquipmentRepository _equipment = Substitute.For<IEquipmentRepository>();

    [Fact]
    public async Task Gear_is_resolved_with_one_batched_lookup_and_unknown_ids_are_skipped()
    {
        _profiles.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(new CharacterProfile
        {
            UserId = "user-1",
            CharacterName = "Test",
            ActiveJob = "CRP",
            ActiveJobLevel = 90,
            Source = ProfileSource.Manual,
            CurrentGear = new Dictionary<string, int> { ["MainHand"] = 1, ["Head"] = 2, ["Body"] = 3 },
        });
        _equipment.GetByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment>
            {
                new() { EquipmentId = 1, Name = "Saw", ItemLevel = 100 },
                new() { EquipmentId = 2, Name = "Hat", ItemLevel = 90 },
            });

        var result = await new GetProfileQueryHandler(_profiles, _equipment)
            .Handle(new GetProfileQuery("user-1"), CancellationToken.None);

        result!.CurrentGear.Should().HaveCount(2);
        result.CurrentGear!.Select(g => g.SlotKey).Should().BeEquivalentTo(["MainHand", "Head"]);
        await _equipment.Received(1).GetByIdsAsync(
            Arg.Is<IEnumerable<int>>(ids => ids.OrderBy(i => i).SequenceEqual(new[] { 1, 2, 3 })),
            Arg.Any<CancellationToken>());
        await _equipment.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }
}
