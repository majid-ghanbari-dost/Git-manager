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
    private readonly ProjectOperationJournal _operationJournal = new();
    private readonly ProjectOperationRecoveryService _recovery;
    private readonly WorkspaceService _workspace = new();
    private readonly GitProcess _git = new();
    private readonly ProjectBackupService _backups;
    private readonly ProjectRestoreService _restores;
    private ProjectCatalog? _catalog;
    private RepositoryImportService? _importer;
    private string _workspacePath = "";
    private string _repositoryUrl = "";
    private string _selectedCategory = "Unclassified";
    private string _statusMessage = "برای شروع، محل کتابخانه را انتخاب کنید.";
    private ProjectRecord? _selectedProject;

    public ObservableCollection<string> Categories { get; } = new() { "Unclassified", "Kandoo", "Business Tools", "Experiments", "Personal", "Client Work" };
    public ObservableCollection<ProjectRecord> Projects { get; } = [];
    public string WorkspacePath { get => _workspacePath; private set { _workspacePath = value; OnPropertyChanged(); } }
    public string RepositoryUrl { get => _repositoryUrl; set { _repositoryUrl = value; OnPropertyChanged(); CloneCommand.NotifyCanExecuteChanged(); } }
    public string SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value; OnPropertyChanged(); } }
    public int ProjectCount => Projects.Count;
    public ProjectRecord? SelectedProject
    {
        get => _selectedProject;
        set { _selectedProject = value; OnPropertyChanged(); BackupCommand.NotifyCanExecuteChanged(); }
    }

    public RelayCommand ChangeWorkspaceCommand { get; }
    public AsyncRelayCommand CloneCommand { get; }
    public AsyncRelayCommand BackupCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        _recovery = new ProjectOperationRecoveryService(_operationJournal);
        _backups = new ProjectBackupService(_git, _operationJournal);
        _restores = new ProjectRestoreService(_git, _backups);
        ChangeWorkspaceCommand = new RelayCommand(ChooseWorkspace);
        CloneCommand = new AsyncRelayCommand(CloneAsync, () => _catalog is not null && !string.IsNullOrWhiteSpace(RepositoryUrl));
        BackupCommand = new AsyncRelayCommand(BackupSelectedAsync, () => SelectedProject is not null);
        RestoreCommand = new AsyncRelayCommand(RestoreBackupAsync);
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
            var recovery = await _recovery.RecoverAsync();
            _catalog = catalog;
            _importer = new RepositoryImportService(_workspace, catalog, _git, _operationJournal);
            WorkspacePath = Path.GetFullPath(path);
            await _settings.SaveWorkspaceAsync(WorkspacePath);
            await RefreshProjectsAsync();
            StatusMessage = recovery.RequiresAttention > 0
                ? $"کتابخانه آماده است؛ {recovery.RequiresAttention} عملیات نیمه‌تمام برای بررسی دستی علامت‌گذاری شد."
                : recovery.RolledBack > 0
                    ? $"کتابخانه آماده است؛ {recovery.RolledBack} عملیات ناقص به‌صورت امن بازگردانی شد."
                    : "کتابخانه آماده است. مخزن GitHub را برای افزودن وارد کنید.";
            CloneCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task CloneAsync()
    {
        if (_importer is null) return;
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

    private async Task BackupSelectedAsync()
    {
        if (SelectedProject is null) return;
        try
        {
            StatusMessage = $"در حال تهیه نسخه پشتیبان از {SelectedProject.Owner}/{SelectedProject.Repository}...";
            var path = await _backups.CreateBackupAsync(SelectedProject);
            var valid = await _backups.VerifyBackupAsync(path);
            StatusMessage = valid
                ? $"پشتیبان ایجاد و SHA-256 آن تأیید شد: {path}"
                : "پشتیبان ساخته شد، اما بررسی صحت آن موفق نبود؛ آن را قابل اتکا تلقی نکنید.";
        }
        catch (Exception ex) { StatusMessage = $"تهیه پشتیبان ناموفق بود: {ex.Message}"; }
    }

    private async Task RestoreBackupAsync()
    {
        var backupDialog = new OpenFolderDialog { Title = "پوشه پشتیبان (شامل manifest.json) را انتخاب کنید", Multiselect = false };
        if (backupDialog.ShowDialog() != true) return;
        var destinationDialog = new OpenFolderDialog { Title = "پوشه والد برای بازیابی پروژه را انتخاب کنید", Multiselect = false };
        if (destinationDialog.ShowDialog() != true) return;
        try
        {
            StatusMessage = "در حال بررسی SHA-256 و بازیابی در یک پوشه جدید...";
            var restored = await _restores.RestoreToNewContainerAsync(backupDialog.FolderName, destinationDialog.FolderName);
            StatusMessage = $"بازیابی با موفقیت انجام شد؛ نسخه اصلی دست‌نخورده باقی ماند: {restored}";
        }
        catch (Exception ex) { StatusMessage = $"بازیابی ناموفق بود: {ex.Message}"; }
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
