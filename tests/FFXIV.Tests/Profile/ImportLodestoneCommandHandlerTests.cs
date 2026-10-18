using FFXIV.Application.Profile;
using FFXIV.Application.Profile.Commands;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FFXIV.Tests.Profile;

public class ImportLodestoneCommandHandlerTests
{
    private readonly ICharacterProfileRepository _profileRepo = Substitute.For<ICharacterProfileRepository>();
    private readonly IEquipmentRepository _equipmentRepo = Substitute.For<IEquipmentRepository>();
    private readonly ILodestoneService _lodestoneService = Substitute.For<ILodestoneService>();
    private readonly ILogger<ImportLodestoneCommandHandler> _logger = Substitute.For<ILogger<ImportLodestoneCommandHandler>>();
    private readonly ImportLodestoneCommandHandler _handler;

    public ImportLodestoneCommandHandlerTests()
    {
        _handler = new ImportLodestoneCommandHandler(
            _profileRepo, _equipmentRepo, _lodestoneService, _logger);
    }

    private static LodestoneCharacterResult CreateLodestoneData(
        Dictionary<string, int>? jobLevels = null,
        LodestoneAttributes? attributes = null)
    {
        return new LodestoneCharacterResult
        {
            LodestoneId = "12345",
            CharacterName = "Test Character",
            ServerName = "Cactuar",
            DataCenter = "Aether",
            AvatarUrl = "https://example.com/avatar.jpg",
            ActiveJobAbbreviation = "CRP",
            ActiveJobLevel = 90,
            JobLevels = jobLevels ?? new Dictionary<string, int> { ["CRP"] = 90, ["BSM"] = 80 },
            EquippedGear = [],
            Attributes = attributes ?? new LodestoneAttributes
            {
                Craftsmanship = 3000,
                Control = 2800,
                CP = 600,
            },
        };
    }

    [Fact]
    public async Task Handle_NewProfile_CallsCreateAsync()
    {
        var command = new ImportLodestoneCommand("user-1", "Test Character", "Cactuar");
        var lodestoneData = CreateLodestoneData();

        _profileRepo.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns((CharacterProfile?)null);
        _lodestoneService.SearchAndFetchCharacterAsync("Test Character", "Cactuar", Arg.Any<CancellationToken>())
            .Returns(lodestoneData);
        _equipmentRepo.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment>());
        _profileRepo.CreateAsync(Arg.Any<CharacterProfile>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<CharacterProfile>(0));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Profile.Should().NotBeNull();
        result.Profile!.Stats.Craftsmanship.Should().Be(3000);
        result.Profile.Stats.Control.Should().Be(2800);
        result.Profile.Stats.CP.Should().Be(600);
        await _profileRepo.Received(1).CreateAsync(Arg.Any<CharacterProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingProfile_MergesJobLevels_WithNewDictReference()
    {
        var existingProfile = new CharacterProfile
        {
            UserId = "user-1",
            CharacterName = "Old Name",
            ServerName = "Cactuar",
            ActiveJob = "CRP",
            ActiveJobLevel = 80,
            JobLevels = new Dictionary<string, int> { ["CRP"] = 80, ["WVR"] = 70 },
            Source = ProfileSource.Manual,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-7),
        };
        var originalJobLevelsRef = existingProfile.JobLevels;

        var command = new ImportLodestoneCommand("user-1", "Test Character", "Cactuar");
        var lodestoneData = CreateLodestoneData(
            jobLevels: new Dictionary<string, int> { ["CRP"] = 90, ["BSM"] = 80 });

        _profileRepo.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(existingProfile);
        _lodestoneService.SearchAndFetchCharacterAsync("Test Character", "Cactuar", Arg.Any<CancellationToken>())
            .Returns(lodestoneData);
        _equipmentRepo.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment>());
        _profileRepo.UpdateAsync(Arg.Any<CharacterProfile>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<CharacterProfile>(0));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        await _profileRepo.Received(1).UpdateAsync(Arg.Any<CharacterProfile>(), Arg.Any<CancellationToken>());

        // Verify merged levels: CRP updated to 90, WVR kept at 70, BSM added at 80
        existingProfile.JobLevels.Should().ContainKey("CRP").WhoseValue.Should().Be(90);
        existingProfile.JobLevels.Should().ContainKey("WVR").WhoseValue.Should().Be(70);
        existingProfile.JobLevels.Should().ContainKey("BSM").WhoseValue.Should().Be(80);

        // Key fix: JobLevels should be a NEW dict reference (not mutated in place)
        existingProfile.JobLevels.Should().NotBeSameAs(originalJobLevelsRef);
    }

    [Fact]
    public async Task Handle_ExistingProfile_AppliesLodestoneStats()
    {
        var existingProfile = new CharacterProfile
        {
            UserId = "user-1",
            CharacterName = "Old Name",
            ServerName = "Cactuar",
            ActiveJob = "CRP",
            ActiveJobLevel = 80,
            JobLevels = new Dictionary<string, int> { ["CRP"] = 80 },
            Source = ProfileSource.Manual,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-7),
        };

        var command = new ImportLodestoneCommand("user-1", "Test Character", "Cactuar");
        var lodestoneData = CreateLodestoneData(
            attributes: new LodestoneAttributes
            {
                Craftsmanship = 4000,
                Control = 3800,
                CP = 700,
                Gathering = 100,
                Perception = 200,
                GP = 300,
            });

        _profileRepo.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(existingProfile);
        _lodestoneService.SearchAndFetchCharacterAsync("Test Character", "Cactuar", Arg.Any<CancellationToken>())
            .Returns(lodestoneData);
        _equipmentRepo.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment>());
        _profileRepo.UpdateAsync(Arg.Any<CharacterProfile>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<CharacterProfile>(0));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        existingProfile.Craftsmanship.Should().Be(4000);
        existingProfile.Control.Should().Be(3800);
        existingProfile.CP.Should().Be(700);
        existingProfile.Gathering.Should().Be(100);
        existingProfile.Perception.Should().Be(200);
        existingProfile.GP.Should().Be(300);
    }

    [Fact]
    public async Task Handle_WithGear_PopulatesCurrentGear()
    {
        var command = new ImportLodestoneCommand("user-1", "Test Character", "Cactuar");
        var lodestoneData = CreateLodestoneData();
        lodestoneData.EquippedGear =
        [
            new LodestoneGearPiece { SlotName = "MainHand", ItemName = "Test Tool" },
        ];

        var equipment = new Equipment
        {
            EquipmentId = 42,
            Name = "Test Tool",
            ItemLevel = 100,
        };

        _profileRepo.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns((CharacterProfile?)null);
        _lodestoneService.SearchAndFetchCharacterAsync("Test Character", "Cactuar", Arg.Any<CancellationToken>())
            .Returns(lodestoneData);
        _equipmentRepo.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { equipment });
        _profileRepo.CreateAsync(Arg.Any<CharacterProfile>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<CharacterProfile>(0));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();

        var capturedProfile = _profileRepo.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CreateAsync")
            .Select(c => c.GetArguments()[0] as CharacterProfile)
            .First();

        capturedProfile!.CurrentGear.Should().NotBeNull();
        capturedProfile.CurrentGear!["MainHand"].Should().Be(42);
    }

    [Fact]
    public async Task Handle_MalformedLodestoneId_IsRejectedBeforeAnythingIsStored()
    {
        var command = new ImportLodestoneCommand("user-1", "Test Character", "Cactuar");
        var lodestoneData = CreateLodestoneData();
        lodestoneData.LodestoneId = "12345; drop";

        _profileRepo.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns((CharacterProfile?)null);
        _lodestoneService.SearchAndFetchCharacterAsync("Test Character", "Cactuar", Arg.Any<CancellationToken>())
            .Returns(lodestoneData);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(502);
        await _profileRepo.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
        await _profileRepo.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Handle_CharacterLinkedToAnotherAccount_Returns409()
    {
        var command = new ImportLodestoneCommand("user-1", "Test Character", "Cactuar");
        var lodestoneData = CreateLodestoneData();

        _profileRepo.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns((CharacterProfile?)null);
        _profileRepo.GetByLodestoneIdAsync("12345", Arg.Any<CancellationToken>())
            .Returns(new CharacterProfile { UserId = "someone-else", CharacterName = "Test Character", ActiveJob = "CRP" });
        _lodestoneService.SearchAndFetchCharacterAsync("Test Character", "Cactuar", Arg.Any<CancellationToken>())
            .Returns(lodestoneData);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        await _profileRepo.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ReimportOfOwnCharacter_IsAllowed()
    {
        var existingProfile = new CharacterProfile
        {
            UserId = "user-1",
            CharacterName = "Test Character",
            LodestoneId = "12345",
            ActiveJob = "CRP",
            ActiveJobLevel = 80,
            JobLevels = new Dictionary<string, int>(),
            Source = ProfileSource.Lodestone,
            LastLodestoneSync = DateTimeOffset.UtcNow.AddDays(-1),
        };
        var command = new ImportLodestoneCommand("user-1", "Test Character", "Cactuar");

        _profileRepo.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(existingProfile);
        _profileRepo.GetByLodestoneIdAsync("12345", Arg.Any<CancellationToken>()).Returns(existingProfile);
        _lodestoneService.SearchAndFetchCharacterAsync("Test Character", "Cactuar", Arg.Any<CancellationToken>())
            .Returns(CreateLodestoneData());
        _equipmentRepo.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment>());
        _profileRepo.UpdateAsync(Arg.Any<CharacterProfile>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<CharacterProfile>(0));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        await _profileRepo.Received(1).UpdateAsync(Arg.Any<CharacterProfile>(), Arg.Any<CancellationToken>());
    }
}
