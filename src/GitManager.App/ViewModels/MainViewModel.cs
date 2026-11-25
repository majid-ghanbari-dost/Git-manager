using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using GitManager.App.Models;
using GitManager.App.Services;
using Microsoft.Win32;

namespace GitManager.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settings;
    private readonly ProjectOperationJournal _operationJournal;
    private readonly GitProcess _git;
    private readonly ProjectOperationLock? _operationLock;
    private readonly ProjectBackupService _backups;
    private readonly ProjectRestoreService _restores;
    private readonly RepositoryPublishService _publisher;
    private ProjectCatalog? _catalog;
    private RepositoryImportService? _importer;
    private CategoryManagementService? _categoryManagement;
    private ProjectCategoryMoveService? _projectCategoryMover;
    private ProjectLifecycleService? _lifecycle;
    private bool _sensitiveOperationsLimited;
    private bool _journalUsable;
    private readonly List<ProjectRecord> _allProjects = [];
    private string _newCategoryName = "";
    private string _renameCategoryName = "";
    private string _workspacePath = "";
    private string _repositoryUrl = "";
    private string _selectedCategory = "Unclassified";
    private string _statusMessage = "برای شروع، محل کتابخانه را انتخاب کنید.";
    private string _searchText = "";
    private string _selectedLifecycleFilter = "همه";
    private ProjectRecord? _selectedProject;

    public ObservableCollection<string> Categories { get; } = new() { "Unclassified", "Kandoo", "Business Tools", "Experiments", "Personal", "Client Work" };
    public ObservableCollection<string> LifecycleFilters { get; } = ["همه", "فعال", "معلق", "بایگانی‌شده", "مفقود/ناسازگار"];
    public ObservableCollection<ProjectRecord> Projects { get; } = [];
    public string WorkspacePath { get => _workspacePath; private set { _workspacePath = value; OnPropertyChanged(); } }
    public string RepositoryUrl
    {
        get => _repositoryUrl;
        set
        {
            _repositoryUrl = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CloneDisabledReason));
            CloneCommand.NotifyCanExecuteChanged();
        }
    }
    public string CloneDisabledReason =>
        CloneCommandGate.DisabledReason(_catalog is not null, _journalUsable, RepositoryUrl);
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            RenameCategoryCommand.NotifyCanExecuteChanged();
            DeleteCategoryCommand.NotifyCanExecuteChanged();
            MoveProjectCommand.NotifyCanExecuteChanged();
            MoveCategoryUpCommand.NotifyCanExecuteChanged();
            MoveCategoryDownCommand.NotifyCanExecuteChanged();
        }
    }
    public string NewCategoryName { get => _newCategoryName; set { _newCategoryName = value; OnPropertyChanged(); CreateCategoryCommand.NotifyCanExecuteChanged(); } }
    public string RenameCategoryName { get => _renameCategoryName; set { _renameCategoryName = value; OnPropertyChanged(); RenameCategoryCommand.NotifyCanExecuteChanged(); } }
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value; OnPropertyChanged(); } }
    public int ProjectCount => Projects.Count;
    public ProjectRecord? SelectedProject
    {
        get => _selectedProject;
        set
        {
            _selectedProject = value;
            OnPropertyChanged();
            BackupCommand.NotifyCanExecuteChanged();
            MoveProjectCommand.NotifyCanExecuteChanged();
            MoveToActiveCommand.NotifyCanExecuteChanged();
            MoveToOnHoldCommand.NotifyCanExecuteChanged();
            ArchiveProjectCommand.NotifyCanExecuteChanged();
            RestoreArchivedCommand.NotifyCanExecuteChanged();
            PushCommand.NotifyCanExecuteChanged();
        }
    }
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); ApplyProjectFilter(); }
    }
    public string SelectedLifecycleFilter
    {
        get => _selectedLifecycleFilter;
        set { _selectedLifecycleFilter = value; OnPropertyChanged(); ApplyProjectFilter(); }
    }

    public RelayCommand ChangeWorkspaceCommand { get; }
    public AsyncRelayCommand CloneCommand { get; }
    public AsyncRelayCommand BackupCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }
    public AsyncRelayCommand CreateCategoryCommand { get; }
    public AsyncRelayCommand RenameCategoryCommand { get; }
    public AsyncRelayCommand DeleteCategoryCommand { get; }
    public AsyncRelayCommand MoveProjectCommand { get; }
    public AsyncRelayCommand MoveCategoryUpCommand { get; }
    public AsyncRelayCommand MoveCategoryDownCommand { get; }
    public AsyncRelayCommand MoveToActiveCommand { get; }
    public AsyncRelayCommand MoveToOnHoldCommand { get; }
    public AsyncRelayCommand ArchiveProjectCommand { get; }
    public AsyncRelayCommand RestoreArchivedCommand { get; }
    public AsyncRelayCommand PushCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel() : this(new SettingsService(), new ProjectOperationJournal(), new GitProcess())
    {
    }

    internal MainViewModel(
        SettingsService settings,
        ProjectOperationJournal journal,
        GitProcess git,
        ProjectOperationLock? operationLock = null)
    {
        _settings = settings;
        _operationJournal = journal;
        _git = git;
        _operationLock = operationLock ?? new ProjectOperationLock();
        _backups = new ProjectBackupService(_git, _operationJournal);
        _restores = new ProjectRestoreService(_git, _backups);
        _publisher = new RepositoryPublishService(_git, _operationJournal, _operationLock);
        ChangeWorkspaceCommand = new RelayCommand(ChooseWorkspace);
        CloneCommand = new AsyncRelayCommand(CloneAsync, CanClone);
        BackupCommand = new AsyncRelayCommand(BackupSelectedAsync, () => CanRunSensitive() && SelectedProject is not null);
        RestoreCommand = new AsyncRelayCommand(RestoreBackupAsync, CanRunSensitive);
        CreateCategoryCommand = new AsyncRelayCommand(CreateCategoryAsync, () => _categoryManagement is not null && !string.IsNullOrWhiteSpace(NewCategoryName));
        RenameCategoryCommand = new AsyncRelayCommand(RenameCategoryAsync,
            () => CanRunSensitive() && _categoryManagement is not null && !string.IsNullOrWhiteSpace(SelectedCategory) && !string.IsNullOrWhiteSpace(RenameCategoryName));
        DeleteCategoryCommand = new AsyncRelayCommand(DeleteCategoryAsync, () => _categoryManagement is not null && !string.IsNullOrWhiteSpace(SelectedCategory));
        MoveProjectCommand = new AsyncRelayCommand(MoveSelectedProjectAsync,
            () => CanRunSensitive() && _projectCategoryMover is not null && SelectedProject is not null && !string.IsNullOrWhiteSpace(SelectedCategory));
        MoveCategoryUpCommand = new AsyncRelayCommand(() => ReorderCategoryAsync(-1), CanMoveCategoryUp);
        MoveCategoryDownCommand = new AsyncRelayCommand(() => ReorderCategoryAsync(1), CanMoveCategoryDown);
        MoveToActiveCommand = new AsyncRelayCommand(() => ChangeLifecycleAsync(LifecycleState.Active),
            () => CanRunSensitive() && _lifecycle is not null && SelectedProject is not null);
        MoveToOnHoldCommand = new AsyncRelayCommand(() => ChangeLifecycleAsync(LifecycleState.OnHold),
            () => CanRunSensitive() && _lifecycle is not null && SelectedProject is not null);
        ArchiveProjectCommand = new AsyncRelayCommand(() => ChangeLifecycleAsync(LifecycleState.Archived),
            () => CanRunSensitive() && _lifecycle is not null && SelectedProject is not null);
        RestoreArchivedCommand = new AsyncRelayCommand(() => ChangeLifecycleAsync(LifecycleState.Active),
            () => CanRunSensitive() && _lifecycle is not null && SelectedProject is not null);
        PushCommand = new AsyncRelayCommand(PushSelectedAsync,
            () => CanRunSensitive() && SelectedProject is not null && SelectedProject.Availability == "Available");
        Projects.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ProjectCount));
    }

    internal bool IsCatalogOpen => _catalog is not null;
    internal bool SensitiveOperationsLimited => _sensitiveOperationsLimited;
    internal bool JournalUsable => _journalUsable;
    internal Task OpenWorkspaceAsync(string path) => SetWorkspaceAsync(path);

    private bool CanClone() => CloneCommandGate.CanExecute(_catalog is not null, _journalUsable, RepositoryUrl);
    private bool CanRunSensitive() => _catalog is not null && !_sensitiveOperationsLimited;

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
            var opened = await LibraryStartup.OpenAsync(path, _operationJournal, _git, _operationLock);
            _categoryManagement = opened.CategoryManagement;
            _projectCategoryMover = opened.ProjectCategoryMover;
            _lifecycle = opened.Lifecycle;
            _catalog = opened.Catalog;
            _importer = opened.Importer;
            _sensitiveOperationsLimited = opened.RecoveryLimited;
            _journalUsable = opened.JournalUsable;
            await RefreshCategoriesAsync();
            WorkspacePath = Path.GetFullPath(path);
            await _settings.SaveWorkspaceAsync(WorkspacePath);
            await RefreshProjectsAsync();
            var missing = _allProjects.Count(x => x.Availability is LifecycleState.Missing or LifecycleState.PathDrift);
            StatusMessage = opened.StatusMessage(missing);
            OnPropertyChanged(nameof(CloneDisabledReason));
            CloneCommand.NotifyCanExecuteChanged();
            BackupCommand.NotifyCanExecuteChanged();
            RestoreCommand.NotifyCanExecuteChanged();
            CreateCategoryCommand.NotifyCanExecuteChanged();
            RenameCategoryCommand.NotifyCanExecuteChanged();
            DeleteCategoryCommand.NotifyCanExecuteChanged();
            MoveProjectCommand.NotifyCanExecuteChanged();
            MoveCategoryUpCommand.NotifyCanExecuteChanged();
            MoveCategoryDownCommand.NotifyCanExecuteChanged();
            MoveToActiveCommand.NotifyCanExecuteChanged();
            MoveToOnHoldCommand.NotifyCanExecuteChanged();
            ArchiveProjectCommand.NotifyCanExecuteChanged();
            RestoreArchivedCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write(DiagnosticLog.DirectoryFor(path), "workspace", "library open failed", ex);
            StatusMessage = "باز کردن کتابخانه انجام نشد. جزئیات در گزارش فنی ذخیره شد.";
        }
    }

    private bool CanMoveCategoryUp()
    {
        var index = Categories.IndexOf(SelectedCategory);
        return _categoryManagement is not null && index > 1;
    }

    private bool CanMoveCategoryDown()
    {
        var index = Categories.IndexOf(SelectedCategory);
        return _categoryManagement is not null && index > 0 && index < Categories.Count - 1;
    }

    private async Task ReorderCategoryAsync(int direction)
    {
        if (_categoryManagement is null) return;
        try
        {
            var category = (await _categoryManagement.GetCategoriesAsync())
                .FirstOrDefault(x => x.Name.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
            if (category is null) throw new InvalidOperationException("دسته‌بندی انتخاب‌شده پیدا نشد.");
            if (direction < 0) await _categoryManagement.MoveUpAsync(category.CategoryId);
            else await _categoryManagement.MoveDownAsync(category.CategoryId);
            await RefreshCategoriesAsync();
            StatusMessage = "ترتیب دسته‌بندی‌ها ذخیره شد.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task MoveSelectedProjectAsync()
    {
        if (_projectCategoryMover is null || SelectedProject is null) return;
        try
        {
            var category = (await _categoryManagement!.GetCategoriesAsync())
                .FirstOrDefault(x => x.Name.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
            if (category is null) throw new InvalidOperationException("دسته‌بندی مقصد پیدا نشد.");
            var destination = await _projectCategoryMover.MoveProjectAsync(SelectedProject.ProjectId, category.CategoryId);
            await RefreshProjectsAsync();
            StatusMessage = $"پروژه با تمام محتویات پوشه‌اش به دسته «{category.Name}» منتقل شد: {destination}";
        }
        catch (Exception ex) { StatusMessage = $"انتقال پروژه انجام نشد: {ex.Message}"; }
    }

    private async Task RefreshCategoriesAsync()
    {
        if (_categoryManagement is null) return;
        var selected = SelectedCategory;
        Categories.Clear();
        foreach (var category in await _categoryManagement.GetCategoriesAsync())
            Categories.Add(category.Name);
        SelectedCategory = Categories.Contains(selected) ? selected : Categories.FirstOrDefault() ?? "Unclassified";
    }

    private async Task CreateCategoryAsync()
    {
        if (_categoryManagement is null) return;
        try
        {
            var category = await _categoryManagement.CreateAsync(NewCategoryName);
            await RefreshCategoriesAsync();
            SelectedCategory = category.Name;
            NewCategoryName = "";
            StatusMessage = $"دسته‌بندی «{category.Name}» ایجاد شد.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task RenameCategoryAsync()
    {
        if (_categoryManagement is null) return;
        try
        {
            var category = (await _categoryManagement.GetCategoriesAsync())
                .FirstOrDefault(x => x.Name.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
            if (category is null) throw new InvalidOperationException("دسته‌بندی انتخاب‌شده پیدا نشد.");
            await _categoryManagement.RenameAsync(category.CategoryId, RenameCategoryName);
            await RefreshCategoriesAsync();
            await RefreshProjectsAsync();
            SelectedCategory = RenameCategoryName.Trim();
            RenameCategoryName = "";
            StatusMessage = "نام دسته‌بندی تغییر کرد.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task DeleteCategoryAsync()
    {
        if (_categoryManagement is null) return;
        try
        {
            var category = (await _categoryManagement.GetCategoriesAsync())
                .FirstOrDefault(x => x.Name.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
            if (category is null) throw new InvalidOperationException("دسته‌بندی انتخاب‌شده پیدا نشد.");
            await _categoryManagement.DeleteAsync(category.CategoryId);
            await RefreshCategoriesAsync();
            StatusMessage = "دسته‌بندی خالی حذف شد؛ هیچ پروژه‌ای حذف نشده است.";
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
            RepositoryUrl = "";
            await RefreshProjectsAsync();
            StatusMessage = $"پروژه {project.Owner}/{project.Repository} با موفقیت ثبت شد.";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write(DiagnosticLog.DirectoryFor(WorkspacePath), "clone", "clone failed", ex);
            StatusMessage = ex is InvalidOperationException
                ? ex.Message
                : "دریافت مخزن انجام نشد. جزئیات در گزارش فنی ذخیره شد.";
        }
    }

    private async Task PushSelectedAsync()
    {
        var project = SelectedProject;
        if (project is null) return;

        var confirmation = MessageBox.Show(
            $"شاخه فعلی پروژه {project.Owner}/{project.Repository} به origin در GitHub ارسال می‌شود.\n\n" +
            "فقط Commitها ارسال می‌شوند؛ تغییرات Commit‌نشده روی رایانه باقی می‌مانند. " +
            "این عملیات Force Push نیست و تاریخچه remote را بازنویسی نمی‌کند.\n\nادامه می‌دهید؟",
            "تأیید ارسال به GitHub", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            StatusMessage = $"در حال ارسال شاخه {project.CurrentBranch} به GitHub...";
            var result = await _publisher.PushAsync(project);
            StatusMessage = $"Push شاخه {result.Branch} با موفقیت انجام شد. Commit: {result.Commit[..Math.Min(8, result.Commit.Length)]}" +
                (result.HasUncommittedChanges
                    ? " — توجه: تغییرات Commit‌نشده فقط روی همین رایانه باقی ماندند."
                    : "");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write(DiagnosticLog.DirectoryFor(WorkspacePath), "push", "push operation failed", ex);
            StatusMessage = ex is InvalidOperationException or TimeoutException
                ? $"ارسال انجام نشد: {ex.Message}"
                : "ارسال به GitHub ناموفق بود؛ نتیجه remote را بررسی کنید. جزئیات در گزارش فنی ذخیره شد.";
        }
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

    private async Task ChangeLifecycleAsync(string target)
    {
        if (_lifecycle is null || SelectedProject is null) return;
        try
        {
            var project = SelectedProject;
            var options = new LifecycleChangeOptions();
            if (LifecycleState.RequiresArchiveConfirm(project.Lifecycle, target))
            {
                var extra = "";
                try
                {
                    var inspect = await _lifecycle.InspectGitAsync(project.RepositoryPath, CancellationToken.None);
                    if (inspect.Dirty)
                        extra = "\nاین مخزن دارای تغییرات Commit‌نشده یا فایل ردیابی‌نشده است. بایگانی این تغییرات را حذف نمی‌کند.";
                    options = options with { ConfirmDirtyWorkingTree = inspect.Dirty };
                }
                catch { }
                if (!Confirm("بایگانی پروژه",
                        $"پروژه {project.Owner}/{project.Repository} به پوشه 03_Archived منتقل می‌شود.\n" +
                        "مخزن Git، تاریخچه، فایل‌های ذخیره‌نشده، پشتیبان‌ها و یادداشت‌ها حذف نمی‌شوند." + extra +
                        "\nادامه می‌دهید؟"))
                    return;
                options = options with { ConfirmArchive = true, ConfirmDirtyWorkingTree = true };
            }
            else if (LifecycleState.RequiresRestoreConfirm(project.Lifecycle, target))
            {
                if (!Confirm("بازگردانی از بایگانی",
                        $"پروژه {project.Owner}/{project.Repository} از بایگانی به {target} برمی‌گردد. مخزن حذف نمی‌شود. ادامه می‌دهید؟"))
                    return;
                options = options with { ConfirmRestoreFromArchive = true, ConfirmDirtyWorkingTree = true };
            }
            else
            {
                try
                {
                    var inspect = await _lifecycle.InspectGitAsync(project.RepositoryPath, CancellationToken.None);
                    if (inspect.Dirty && !Confirm("تغییرات Commit‌نشده",
                            "این مخزن دارای تغییرات Commit‌نشده است. انتقال وضعیت آن‌ها را حذف نمی‌کند. ادامه می‌دهید؟"))
                        return;
                    options = options with { ConfirmDirtyWorkingTree = true };
                }
                catch { options = options with { ConfirmDirtyWorkingTree = true }; }
            }

            await _lifecycle.ChangeLifecycleAsync(project.ProjectId, target, options);
            await RefreshProjectsAsync();
            StatusMessage = $"وضعیت پروژه به «{target}» تغییر کرد.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private static bool Confirm(string title, string text) =>
        MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private async Task RefreshProjectsAsync()
    {
        if (_catalog is null) return;
        _allProjects.Clear();
        _allProjects.AddRange(await _catalog.GetProjectsAsync());
        ApplyProjectFilter();
    }

    private void ApplyProjectFilter()
    {
        var selectedId = SelectedProject?.ProjectId;
        Projects.Clear();
        foreach (var project in _allProjects.Where(MatchesFilter))
            Projects.Add(project);
        SelectedProject = Projects.FirstOrDefault(x => x.ProjectId == selectedId) ?? Projects.FirstOrDefault();
        OnPropertyChanged(nameof(ProjectCount));
    }

    private bool MatchesFilter(ProjectRecord project)
    {
        var filter = SelectedLifecycleFilter;
        var ok = filter switch
        {
            "فعال" => project.Lifecycle == LifecycleState.Active,
            "معلق" => project.Lifecycle == LifecycleState.OnHold,
            "بایگانی‌شده" => project.Lifecycle == LifecycleState.Archived,
            "مفقود/ناسازگار" => project.Availability is LifecycleState.Missing or LifecycleState.PathDrift,
            _ => true
        };
        if (!ok) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var q = SearchText.Trim();
        return project.Repository.Contains(q, StringComparison.OrdinalIgnoreCase)
            || project.Owner.Contains(q, StringComparison.OrdinalIgnoreCase)
            || project.Category.Contains(q, StringComparison.OrdinalIgnoreCase)
            || project.CanonicalRemoteUrl.Contains(q, StringComparison.OrdinalIgnoreCase)
            || project.Lifecycle.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
