using GitManager.App.Models;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class ProjectAvailabilityTests
{
    [Fact]
    public async Task Probe_MarksMissingWithoutCreatingFolders()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            Directory.Delete(project.ContainerPath, recursive: true);
            var probe = new ProjectAvailabilityService(root, catalog);
            Assert.Equal(LifecycleState.Missing, await probe.ProbeAndStoreAsync(project));
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            Assert.False(Directory.Exists(project.ContainerPath));
            Assert.False(Directory.Exists(dest));
            Assert.Equal(LifecycleState.Missing, Assert.Single(await catalog.GetProjectsAsync()).Availability);
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Probe_MarksPathDriftWhenContainerIsNotCanonical()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            var drifted = Path.Combine(root, "elsewhere", "repo");
            Directory.CreateDirectory(Path.GetDirectoryName(drifted)!);
            Directory.Move(project.ContainerPath, drifted);
            await catalog.CommitLifecycleMoveAsync(
                project.ProjectId, LifecycleState.Active, drifted, Path.Combine(drifted, "repository"),
                new LifecycleEventRecord(0, Guid.NewGuid().ToString("N"), project.ProjectId, "Active", "Active",
                    null, LifecycleState.CatalogCommitted, null, null, "setup", DateTime.UtcNow));
            var saved = Assert.Single(await catalog.GetProjectsAsync());
            var probe = new ProjectAvailabilityService(root, catalog);
            Assert.Equal(LifecycleState.PathDrift, await probe.ProbeAndStoreAsync(saved));
            Assert.Equal(LifecycleState.PathDrift, Assert.Single(await catalog.GetProjectsAsync()).Availability);
            Assert.True(Directory.Exists(drifted));
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Probe_DoesNotMoveOrDelete()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            var marker = Path.Combine(project.ContainerPath, "notes", "x.txt");
            await File.WriteAllTextAsync(marker, "keep");
            await new ProjectAvailabilityService(root, catalog).ProbeAllAsync();
            Assert.True(File.Exists(marker));
            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.Equal(LifecycleState.Available, Assert.Single(await catalog.GetProjectsAsync()).Availability);
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }
}
