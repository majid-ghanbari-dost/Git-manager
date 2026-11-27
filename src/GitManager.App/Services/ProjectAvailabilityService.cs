using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class ProjectAvailabilityService(string workspaceRoot, ProjectCatalog catalog)
{
    private readonly string _workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly WorkspaceService _workspace = new();

    public async Task<string> ProbeAndStoreAsync(ProjectRecord project, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var availability = Probe(project);
        await catalog.UpdateAvailabilityAsync(
            project.ProjectId, availability,
            availability == LifecycleState.Available ? DateTime.UtcNow : null);
        project.Availability = availability;
        if (availability == LifecycleState.Available) project.LastSeenAtUtc = DateTime.UtcNow;
        return availability;
    }

    public async Task ProbeAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var project in await catalog.GetProjectsAsync())
            await ProbeAndStoreAsync(project, cancellationToken);
    }

    public string Probe(ProjectRecord project)
    {
        try
        {
            if (!LifecycleState.IsKnown(project.Lifecycle))
                return LifecycleState.PathDrift;

            var expected = Path.GetFullPath(_workspace.GetProjectContainer(
                _workspaceRoot, project.Lifecycle, project.Category, project.Owner, project.Repository));
            var container = string.IsNullOrWhiteSpace(project.ContainerPath)
                ? "" : Path.GetFullPath(project.ContainerPath);
            var repository = string.IsNullOrWhiteSpace(project.RepositoryPath)
                ? "" : Path.GetFullPath(project.RepositoryPath);

            if (!PathSafety.IsWithin(_workspaceRoot, expected))
                return LifecycleState.PathDrift;
            if (string.IsNullOrWhiteSpace(container) || !Directory.Exists(container) || !Directory.Exists(repository))
                return LifecycleState.Missing;
            if (!container.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
                !repository.Equals(Path.Combine(container, "repository"), StringComparison.OrdinalIgnoreCase))
                return LifecycleState.PathDrift;
            return LifecycleState.Available;
        }
        catch (InvalidOperationException)
        {
            return LifecycleState.PathDrift;
        }
    }
}
