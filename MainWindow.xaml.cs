using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using РасчетВыплатЗарплаты.Models;
using РасчетВыплатЗарплаты.Services;
using РасчетВыплатЗарплаты.ViewModels;

namespace РасчетВыплатЗарплаты;

public partial class MainWindow : Window
{
    private SalaryInput _salaryInput;
    private ConfigService _configService;

    public MainWindow()
    {
        InitializeComponent();
        _configService = new ConfigService();
        _salaryInput = new SalaryInput
        {
            CalculationDate = DateTime.Today,
            MonthlySalary = 300000,
            SalaryType = SalaryType.Net,
            AdvancePayDay = 20,
            SettlementPayDay = 5,
            ProbationSalary = 280000,
            ProbationPeriodMonths = 3,
            HireDate = new DateTime(2024, 7, 22),
            BaseSalary = 300000,
            CalculateIndexationUnderpayments = false,
            CalculateUnusedVacationCompensation = false,
            HolidayWorkDailyRateMethod = HolidayWorkDailyRateMethod.MonthlyWorkDays,
            IndexationRules = new List<IndexationRule>
            {
                new IndexationRule
                {
                    Date = new DateTime(2025, 3, 1),
                    Percent = 9.57m,
                    IsPerformed = false
                },
                new IndexationRule
                {
                    Date = new DateTime(2026, 3, 1),
                    Percent = 5.59m,
                    IsPerformed = false
                }
            },
            HolidayWorkDates = new List<DateTime>
            {
                new DateTime(2025, 12, 31),
                new DateTime(2026, 1, 1),
                new DateTime(2026, 1, 2)
            }
        };
        LoadConfigAutomatically();
        LoadDataToForm();
        SetupDataGridDefaults();
    }
    
    private void LoadConfigAutomatically()
    {
        try
        {
            var appDirectory = GetApplicationDirectory();
            var configPath = Path.Combine(appDirectory, "config.json");
            var templatePath = Path.Combine(appDirectory, "config.template.json");
            
            string? pathToLoad = null;
            if (File.Exists(configPath))
            {
                pathToLoad = configPath;
            }
            else if (File.Exists(templatePath))
            {
                pathToLoad = templatePath;
            }
            
            if (pathToLoad != null)
            {
                var config = _configService.LoadConfig(pathToLoad);
                _salaryInput = _configService.ConvertToSalaryInput(config);
            }
        }
        catch (Exception ex)
        {
            var errorWindow = new ErrorWindow("Ошибка автоматической загрузки конфигурации", 
                $"Не удалось автоматически загрузить конфигурацию:\n{ex.Message}\n\nДетали:\n{ex}");
            errorWindow.Owner = this;
            errorWindow.ShowDialog();
        }
    }
    
    private static string GetApplicationDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(processPath));
            if (!string.IsNullOrEmpty(dir))
                return dir;
        }
        
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            var dir = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (dir.Length > 0)
                return dir;
        }
        
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void SetupDataGridDefaults()
    {
        IndexationDataGrid.InitializingNewItem += (s, e) =>
        {
            if (e.NewItem is IndexationRuleViewModel item)
            {
                item.Date = new DateTime(DateTime.Now.Year, 1, 1);
            }
        };

        SickLeaveFromPicker.SelectedDateChanged += (s, e) =>
        {
            if (SickLeaveFromPicker.SelectedDate.HasValue && 
                (!SickLeaveToPicker.SelectedDate.HasValue || SickLeaveToPicker.SelectedDate.Value < SickLeaveFromPicker.SelectedDate.Value))
            {
                SickLeaveToPicker.SelectedDate = SickLeaveFromPicker.SelectedDate.Value;
            }
        };

        VacationFromPicker.SelectedDateChanged += (s, e) =>
        {
            if (VacationFromPicker.SelectedDate.HasValue && 
                (!VacationToPicker.SelectedDate.HasValue || VacationToPicker.SelectedDate.Value < VacationFromPicker.SelectedDate.Value))
            {
                VacationToPicker.SelectedDate = VacationFromPicker.SelectedDate.Value;
            }
        };

    }

    private void CalculationDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CalculationDatePicker.SelectedDate.HasValue)
        {
            if (DismissalDatePicker.SelectedDate == null || DismissalDatePicker.SelectedDate.Value < CalculationDatePicker.SelectedDate.Value)
            {
                DismissalDatePicker.SelectedDate = CalculationDatePicker.SelectedDate.Value;
            }
        }
        UpdateCalculateButtonState();
    }

    private void UpdateCalculateButtonState()
    {
        CalculateButton.IsEnabled = CalculationDatePicker.SelectedDate.HasValue;
    }

    private void CalculateUnusedVacationCompensationCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        DismissalDateGrid.Visibility = CalculateUnusedVacationCompensationCheckBox.IsChecked == true 
            ? Visibility.Visible 
            : Visibility.Collapsed;
        
        if (CalculateUnusedVacationCompensationCheckBox.IsChecked == true && !DismissalDatePicker.SelectedDate.HasValue)
        {
            DismissalDatePicker.SelectedDate = CalculationDatePicker.SelectedDate ?? DateTime.Today;
        }
    }

    private void LoadDataToForm()
    {
        MonthlySalaryTextBox.Text = _salaryInput.MonthlySalary.ToString("F2", CultureInfo.InvariantCulture);
        SalaryTypeComboBox.SelectedIndex = _salaryInput.SalaryType == SalaryType.Net ? 0 : 1;
        AdvancePayDayTextBox.Text = _salaryInput.AdvancePayDay.ToString();
        SettlementPayDayTextBox.Text = _salaryInput.SettlementPayDay.ToString();
        ProbationSalaryTextBox.Text = _salaryInput.ProbationSalary?.ToString("F2", CultureInfo.InvariantCulture) ?? "";
        ProbationPeriodTextBox.Text = _salaryInput.ProbationPeriodMonths?.ToString() ?? "";
        HireDatePicker.SelectedDate = _salaryInput.HireDate;
        BaseSalaryTextBox.Text = _salaryInput.BaseSalary?.ToString("F2", CultureInfo.InvariantCulture) ?? "";
        CalculationDatePicker.SelectedDate = _salaryInput.CalculationDate;
        UpdateCalculateButtonState();
        
        var hasUnperformedIndexation = _salaryInput.IndexationRules.Any(r => !r.IsPerformed);
        _salaryInput.CalculateIndexationUnderpayments = hasUnperformedIndexation;
        CalculateUnusedVacationCompensationCheckBox.IsChecked = _salaryInput.CalculateUnusedVacationCompensation;
        DismissalDatePicker.SelectedDate = _salaryInput.DismissalDate ?? _salaryInput.CalculationDate;
        DismissalDateGrid.Visibility = _salaryInput.CalculateUnusedVacationCompensation ? Visibility.Visible : Visibility.Collapsed;
        
        foreach (ComboBoxItem item in HolidayWorkDailyRateMethodComboBox.Items)
        {
            if (item.Tag?.ToString() == _salaryInput.HolidayWorkDailyRateMethod.ToString())
            {
                HolidayWorkDailyRateMethodComboBox.SelectedItem = item;
                break;
            }
        }
        if (HolidayWorkDailyRateMethodComboBox.SelectedItem == null)
        {
            HolidayWorkDailyRateMethodComboBox.SelectedIndex = 0;
        }

        IndexationDataGrid.ItemsSource = new ObservableCollection<IndexationRuleViewModel>(
            _salaryInput.IndexationRules.OrderBy(r => r.Date).Select(r => new IndexationRuleViewModel
            {
                Date = r.Date,
                Percent = r.Percent,
                IsPerformed = r.IsPerformed
            }));

        HolidayWorkListBox.ItemsSource = new ObservableCollection<HolidayWorkViewModel>(
            _salaryInput.HolidayWorkDates.OrderBy(d => d).Select(d => new HolidayWorkViewModel { Date = d }));

        SickLeavesListBox.ItemsSource = new ObservableCollection<SickLeaveViewModel>(
            _salaryInput.SickLeaves.OrderBy(s => s.From).Select(s => new SickLeaveViewModel
            {
                From = s.From,
                To = s.To,
                Amount = s.Amount
            }));

        VacationsListBox.ItemsSource = new ObservableCollection<VacationViewModel>(
            _salaryInput.Vacations.OrderBy(v => v.From).Select(v => new VacationViewModel
            {
                From = v.From,
                To = v.To,
                Amount = v.Amount
            }));

        UpdateExpanderStates();
    }

    private void UpdateExpanderStates()
    {
        if (HolidayWorkListBox.ItemsSource is ObservableCollection<HolidayWorkViewModel> holidayCollection)
        {
            HolidayWorkExpander.IsExpanded = holidayCollection.Count > 0;
        }

        if (SickLeavesListBox.ItemsSource is ObservableCollection<SickLeaveViewModel> sickLeaveCollection)
        {
            SickLeaveExpander.IsExpanded = sickLeaveCollection.Count > 0;
        }

        if (VacationsListBox.ItemsSource is ObservableCollection<VacationViewModel> vacationCollection)
        {
            VacationExpander.IsExpanded = vacationCollection.Count > 0;
        }
    }

    private void SaveDataFromForm()
    {
        if (decimal.TryParse(MonthlySalaryTextBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var monthlySalary))
            _salaryInput.MonthlySalary = monthlySalary;
        
        _salaryInput.SalaryType = SalaryTypeComboBox.SelectedIndex == 0 ? SalaryType.Net : SalaryType.Gross;
        
        if (int.TryParse(AdvancePayDayTextBox.Text, out var advancePayDay))
            _salaryInput.AdvancePayDay = advancePayDay;
        
        if (int.TryParse(SettlementPayDayTextBox.Text, out var settlementPayDay))
            _salaryInput.SettlementPayDay = settlementPayDay;
        
        if (decimal.TryParse(ProbationSalaryTextBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var probationSalary))
            _salaryInput.ProbationSalary = probationSalary;
        else
            _salaryInput.ProbationSalary = null;
        
        if (int.TryParse(ProbationPeriodTextBox.Text, out var probationPeriod))
            _salaryInput.ProbationPeriodMonths = probationPeriod;
        else
            _salaryInput.ProbationPeriodMonths = null;
        
        _salaryInput.HireDate = HireDatePicker.SelectedDate;
        
        if (decimal.TryParse(BaseSalaryTextBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var baseSalary))
            _salaryInput.BaseSalary = baseSalary;
        else
            _salaryInput.BaseSalary = null;
        
        _salaryInput.CalculationDate = CalculationDatePicker.SelectedDate ?? DateTime.Today;
        
        if (!DismissalDatePicker.SelectedDate.HasValue)
        {
            DismissalDatePicker.SelectedDate = _salaryInput.CalculationDate;
        }
        
        if (HolidayWorkDailyRateMethodComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
        {
            if (Enum.TryParse<HolidayWorkDailyRateMethod>(selectedItem.Tag.ToString(), true, out var method))
            {
                _salaryInput.HolidayWorkDailyRateMethod = method;
            }
        }

        _salaryInput.IndexationRules.Clear();
        foreach (var item in IndexationDataGrid.Items)
        {
            if (item is IndexationRuleViewModel viewModel)
            {
                _salaryInput.IndexationRules.Add(new IndexationRule
                {
                    Date = viewModel.Date,
                    Percent = viewModel.Percent,
                    IsPerformed = viewModel.IsPerformed
                });
            }
        }

        var hasUnperformedIndexation = _salaryInput.IndexationRules.Any(r => !r.IsPerformed);
        _salaryInput.CalculateIndexationUnderpayments = hasUnperformedIndexation;
        _salaryInput.CalculateUnusedVacationCompensation = CalculateUnusedVacationCompensationCheckBox.IsChecked == true;
        _salaryInput.DismissalDate = DismissalDatePicker.SelectedDate ?? _salaryInput.CalculationDate;

        _salaryInput.HolidayWorkDates.Clear();
        if (HolidayWorkListBox.ItemsSource is ObservableCollection<HolidayWorkViewModel> holidayCollection)
        {
            foreach (var item in holidayCollection)
            {
                _salaryInput.HolidayWorkDates.Add(item.Date);
            }
        }

        _salaryInput.SickLeaves.Clear();
        if (SickLeavesListBox.ItemsSource is ObservableCollection<SickLeaveViewModel> sickLeaveCollection)
        {
            foreach (var item in sickLeaveCollection)
            {
                _salaryInput.SickLeaves.Add(new SickLeavePeriod
                {
                    From = item.From,
                    To = item.To,
                    Amount = item.Amount
                });
            }
        }

        _salaryInput.Vacations.Clear();
        if (VacationsListBox.ItemsSource is ObservableCollection<VacationViewModel> vacationCollection)
        {
            foreach (var item in vacationCollection)
            {
                _salaryInput.Vacations.Add(new VacationPeriod
                {
                    From = item.From,
                    To = item.To,
                    Amount = item.Amount
                });
            }
        }
    }

    private void LoadConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
            Title = "Загрузить конфигурацию"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var config = _configService.LoadConfig(dialog.FileName);
                _salaryInput = _configService.ConvertToSalaryInput(config);
                LoadDataToForm();
            }
            catch (Exception ex)
            {
                var errorWindow = new ErrorWindow("Ошибка загрузки конфигурации", 
                    $"Ошибка: {ex.Message}\n\nДетали:\n{ex}");
                errorWindow.Owner = this;
                errorWindow.ShowDialog();
            }
        }
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        SaveDataFromForm();
        
        var dialog = new SaveFileDialog
        {
            Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
            Title = "Сохранить конфигурацию",
            FileName = "config.json"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var config = ConvertToConfigData(_salaryInput);
                var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show("Конфигурация сохранена успешно", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var errorWindow = new ErrorWindow("Ошибка сохранения конфигурации", 
                    $"Ошибка: {ex.Message}\n\nДетали:\n{ex}");
                errorWindow.Owner = this;
                errorWindow.ShowDialog();
            }
        }
    }

    private ConfigData ConvertToConfigData(SalaryInput input)
    {
        var config = new ConfigData
        {
            Salary = new SalaryConfig
            {
                MonthlySalary = input.MonthlySalary,
                SalaryType = input.SalaryType == SalaryType.Net ? "Net" : "Gross",
                AdvancePayDay = input.AdvancePayDay,
                SettlementPayDay = input.SettlementPayDay,
                ProbationSalary = input.ProbationSalary,
                ProbationPeriodMonths = input.ProbationPeriodMonths
            },
            Calculation = new CalculationConfig
            {
                CalculationDate = input.CalculationDate.ToString("yyyy-MM-dd"),
                CalculateIndexationUnderpayments = input.CalculateIndexationUnderpayments ? true : null,
                CalculateUnusedVacationCompensation = input.CalculateUnusedVacationCompensation ? true : null,
                DismissalDate = input.DismissalDate?.ToString("yyyy-MM-dd")
            },
            Indexation = new IndexationConfig
            {
                HireDate = input.HireDate?.ToString("yyyy-MM-dd"),
                BaseSalaryNet = input.BaseSalary,
                IndexationEvents = input.IndexationRules.Select(r => new IndexationEventConfig
                {
                    Date = r.Date.ToString("yyyy-MM-dd"),
                    Percent = r.Percent,
                    IsPerformed = r.IsPerformed
                }).ToList()
            },
            HolidayWork = new HolidayWorkConfig
            {
                Dates = input.HolidayWorkDates.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
                DailyRateMethod = input.HolidayWorkDailyRateMethod.ToString()
            },
            SickLeaves = input.SickLeaves.Select(s => new SickLeaveConfig
            {
                From = s.From.ToString("yyyy-MM-dd"),
                To = s.To.ToString("yyyy-MM-dd"),
                Amount = s.Amount
            }).ToList(),
            Vacations = input.Vacations.Select(v => new VacationConfig
            {
                From = v.From.ToString("yyyy-MM-dd"),
                To = v.To.ToString("yyyy-MM-dd"),
                Amount = v.Amount
            }).ToList()
        };
        return config;
    }


    private void CalculateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveDataFromForm();
            var resultsWindow = new ResultsWindow(_salaryInput);
            resultsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            var errorWindow = new ErrorWindow("Ошибка при расчёте", 
                $"Ошибка: {ex.Message}\n\nДетали:\n{ex}");
            errorWindow.Owner = this;
            errorWindow.ShowDialog();
        }
    }

    private void AddHolidayWork_Click(object sender, RoutedEventArgs e)
    {
        if (!HolidayWorkDatePicker.SelectedDate.HasValue)
        {
            MessageBox.Show("Выберите дату", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var date = HolidayWorkDatePicker.SelectedDate.Value;
        
        if (HolidayWorkListBox.ItemsSource is ObservableCollection<HolidayWorkViewModel> collection)
        {
            if (collection.Any(vm => vm.Date.Date == date.Date))
            {
                MessageBox.Show("Эта дата уже добавлена", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            collection.Add(new HolidayWorkViewModel { Date = date });
            var sorted = collection.OrderBy(vm => vm.Date).ToList();
            collection.Clear();
            foreach (var item in sorted)
            {
                collection.Add(item);
            }
        }
    }

    private void RemoveHolidayWork_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is HolidayWorkViewModel item)
        {
            if (HolidayWorkListBox.ItemsSource is ObservableCollection<HolidayWorkViewModel> collection)
            {
                collection.Remove(item);
            }
        }
    }

    private void AddSickLeave_Click(object sender, RoutedEventArgs e)
    {
        if (!SickLeaveFromPicker.SelectedDate.HasValue || !SickLeaveToPicker.SelectedDate.HasValue)
        {
            MessageBox.Show("Выберите даты начала и окончания больничного", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var from = SickLeaveFromPicker.SelectedDate.Value;
        var to = SickLeaveToPicker.SelectedDate.Value;

        if (to < from)
        {
            MessageBox.Show("Дата окончания не может быть раньше даты начала", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(SickLeaveAmountTextBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
        {
            amount = 0;
        }

        if (SickLeavesListBox.ItemsSource is ObservableCollection<SickLeaveViewModel> collection)
        {
            collection.Add(new SickLeaveViewModel 
            { 
                From = from, 
                To = to, 
                Amount = amount 
            });
            var sorted = collection.OrderBy(vm => vm.From).ToList();
            collection.Clear();
            foreach (var item in sorted)
            {
                collection.Add(item);
            }
        }

        SickLeaveAmountTextBox.Text = "0";
    }

    private void RemoveSickLeave_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SickLeaveViewModel item)
        {
            if (SickLeavesListBox.ItemsSource is ObservableCollection<SickLeaveViewModel> collection)
            {
                collection.Remove(item);
            }
        }
    }

    private void AddVacation_Click(object sender, RoutedEventArgs e)
    {
        if (!VacationFromPicker.SelectedDate.HasValue || !VacationToPicker.SelectedDate.HasValue)
        {
            MessageBox.Show("Выберите даты начала и окончания отпуска", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var from = VacationFromPicker.SelectedDate.Value;
        var to = VacationToPicker.SelectedDate.Value;

        if (to < from)
        {
            MessageBox.Show("Дата окончания не может быть раньше даты начала", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(VacationAmountTextBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
        {
            amount = 0;
        }

        if (VacationsListBox.ItemsSource is ObservableCollection<VacationViewModel> collection)
        {
            collection.Add(new VacationViewModel 
            { 
                From = from, 
                To = to, 
                Amount = amount 
            });
            var sorted = collection.OrderBy(vm => vm.From).ToList();
            collection.Clear();
            foreach (var item in sorted)
            {
                collection.Add(item);
            }
        }

        VacationAmountTextBox.Text = "0";
    }

    private void RemoveVacation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VacationViewModel item)
        {
            if (VacationsListBox.ItemsSource is ObservableCollection<VacationViewModel> collection)
            {
                collection.Remove(item);
            }
        }
    }
}
