using FluentAssertions;
using Storage.Domain.Common;
using Storage.Domain.Enums;
using DomainFile = Storage.Domain.Entities.File;

namespace Storage.Domain.Tests;

public class FileStatusTransitionTests
{
    private static DomainFile CreatePendingFile() =>
        DomainFile.Create(Guid.NewGuid(), "test-service", "documents", "test.pdf", "application/pdf", 1024);

    [Theory]
    [InlineData(FileStatus.Pending, FileStatus.Scanning)]
    public void Transition_FromPendingToScanning_Succeeds(FileStatus from, FileStatus to)
    {
        var file = CreatePendingFile();
        // file starts at Pending
        var act = () => file.Transition(to);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(FileStatus.Scanning, FileStatus.Ready)]
    [InlineData(FileStatus.Scanning, FileStatus.Quarantined)]
    public void Transition_FromScanningToReadyOrQuarantined_Succeeds(FileStatus from, FileStatus to)
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        var act = () => file.Transition(to);
        act.Should().NotThrow();
    }

    [Fact]
    public void Transition_FromReadyToDeleted_Succeeds()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Ready);
        var act = () => file.Transition(FileStatus.Deleted);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(FileStatus.Pending, FileStatus.Ready)]
    [InlineData(FileStatus.Pending, FileStatus.Quarantined)]
    [InlineData(FileStatus.Pending, FileStatus.Deleted)]
    public void Transition_InvalidFromPending_ThrowsInvalidStatusTransitionException(FileStatus from, FileStatus to)
    {
        var file = CreatePendingFile();
        var act = () => file.Transition(to);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Transition_FromScanningToPending_Throws()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        var act = () => file.Transition(FileStatus.Pending);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Transition_FromReadyToPending_Throws()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Ready);
        var act = () => file.Transition(FileStatus.Pending);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Transition_FromReadyToScanning_Throws()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Ready);
        var act = () => file.Transition(FileStatus.Scanning);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Transition_FromQuarantinedToReady_Throws()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Quarantined);
        var act = () => file.Transition(FileStatus.Ready);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Theory]
    [InlineData(FileStatus.Pending, FileStatus.Scanning)]
    [InlineData(FileStatus.Scanning, FileStatus.Ready)]
    [InlineData(FileStatus.Scanning, FileStatus.Quarantined)]
    public void Transition_AfterValidTransition_StatusEqualsNewStatus(FileStatus from, FileStatus to)
    {
        DomainFile file;
        if (from == FileStatus.Pending)
        {
            file = CreatePendingFile();
        }
        else
        {
            file = CreatePendingFile();
            file.Transition(FileStatus.Scanning);
        }
        file.Transition(to);
        file.Status.Should().Be(to);
    }

    [Fact]
    public void Transition_ReadyToDeleted_StatusEqualsDeleted()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Ready);
        file.Transition(FileStatus.Deleted);
        file.Status.Should().Be(FileStatus.Deleted);
    }
}
