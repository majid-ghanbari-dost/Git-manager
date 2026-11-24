using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GitManager.App.Models;
using GitManager.App.Services;
using Microsoft.Win32;

namespace GitManager.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settings = new();
    private readonly WorkspaceService _workspace = new();
    private readonly GitProcess _git = new();
    private ProjectCatalog? _catalog;
    private RepositoryImportService? _importer;
    private string _workspacePath = "";
    private string _repositoryUrl = "";
    private string _selectedCategory = "Unclassified";
    private string _statusMessage = "برای شروع، محل کتابخانه را انتخاب کنید.";

    public ObservableCollection<string> Categories { get; } = new() { "Unclassified", "Kandoo", "Business Tools", "Experiments", "Personal", "Client Work" };
    public ObservableCollection<ProjectRecord> Projects { get; } = [];
    public string WorkspacePath { get => _workspacePath; private set { _workspacePath = value; OnPropertyChanged(); } }
    public string RepositoryUrl { get => _repositoryUrl; set { _repositoryUrl = value; OnPropertyChanged(); } }
    public string SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value; OnPropertyChanged(); } }
    public int ProjectCount => Projects.Count;

    public RelayCommand ChangeWorkspaceCommand { get; }
    public AsyncRelayCommand CloneCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        ChangeWorkspaceCommand = new RelayCommand(ChooseWorkspace);
        CloneCommand = new AsyncRelayCommand(CloneAsync, () => _catalog is not null && !string.IsNullOrWhiteSpace(RepositoryUrl));
        Projects.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ProjectCount));
    }

    public async Task InitializeAsync()
    {
        var saved = await _settings.LoadWorkspaceAsync();
        if (!string.IsNullOrWhiteSpace(saved) && Directory.Exists(saved))
            await SetWorkspaceAsync(saved);
        else
            ChooseWorkspace();
    }

    private void ChooseWorkspace()
    {
        var dialog = new OpenFolderDialog { Title = "محل اصلی کتابخانه Git Manager را انتخاب کنید", Multiselect = false };
        if (dialog.ShowDialog() == true)
            _ = SetWorkspaceAsync(dialog.FolderName);
    }

    private async Task SetWorkspaceAsync(string path)
    {
        try
        {
            _workspace.Initialize(path);
            var catalog = new ProjectCatalog(path);
            await catalog.InitializeAsync();
            _catalog = catalog;
            _importer = new RepositoryImportService(_workspace, catalog, _git);
            WorkspacePath = Path.GetFullPath(path);
            await _settings.SaveWorkspaceAsync(WorkspacePath);
            await RefreshProjectsAsync();
            StatusMessage = "کتابخانه آماده است. مخزن GitHub را برای افزودن وارد کنید.";
            CloneCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task CloneAsync()
    {
        if (_importer is null || _catalog is null) return;
        try
        {
            StatusMessage = "در حال بررسی دسترسی و دریافت کامل تاریخچه Git...";
            var project = await _importer.CloneAsync(WorkspacePath, RepositoryUrl, SelectedCategory);
            Projects.Add(project);
            RepositoryUrl = "";
            StatusMessage = $"پروژه {project.Owner}/{project.Repository} با موفقیت ثبت شد.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task RefreshProjectsAsync()
    {
        if (_catalog is null) return;
        Projects.Clear();
        foreach (var project in await _catalog.GetProjectsAsync()) Projects.Add(project);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
