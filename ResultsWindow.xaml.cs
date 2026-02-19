using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Linq;
using РасчетВыплатЗарплаты.Models;
using РасчетВыплатЗарплаты.Services;
using РасчетВыплатЗарплаты.Services.Export;
using РасчетВыплатЗарплаты.Services.Infrastructure;

namespace РасчетВыплатЗарплаты;

public partial class ResultsWindow : Window
{
    private SalaryInput _input;
    private List<PaymentRecord> _records = new();
    private List<VacationPayResult> _vacationPayResults = new();
    private UnusedVacationCompensation? _unusedVacationCompensation;

    public ResultsWindow(SalaryInput input)
    {
        InitializeComponent();
        _input = input;
        Calculate();
    }

    private async void Calculate()
    {
        StatusLabel.Content = "Загрузка данных...";
        
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var hasIndexation = _input.IndexationRules.Any(r => !r.IsPerformed);
            var unperformedIndexations = _input.IndexationRules.Where(r => !r.IsPerformed).ToList();
            var startYear = _input.HireDate?.Year ?? (hasIndexation
                ? unperformedIndexations.Min(r => r.Date).Year
                : _input.CalculationDate.Year);

            StatusLabel.Content = "Загрузка производственного календаря...";
            var calendarService = new ProductionCalendarService(httpClient);
            await calendarService.Load(startYear, _input.CalculationDate.Year);

            CbKeyRateService? keyRateService = null;
            if (_input.CalculateIndexationUnderpayments && hasIndexation)
            {
                var earliestIndexation = unperformedIndexations.Count > 0
                    ? unperformedIndexations.Min(r => r.Date)
                    : _input.BaseIndexationDate ?? _input.CalculationDate.AddMonths(-12);

                StatusLabel.Content = "Загрузка ключевой ставки ЦБ РФ...";
                keyRateService = new CbKeyRateService(httpClient);
                await keyRateService.Load(
                    earliestIndexation.AddMonths(-1),
                    _input.CalculationDate.AddDays(1));
            }

            StatusLabel.Content = "Расчёт...";

            var salaryService = new SalaryCalculationService(calendarService);
            _records = salaryService.Calculate(_input);

            if (_input.CalculateIndexationUnderpayments && keyRateService != null)
            {
                var compensationService = new CompensationCalculationService(keyRateService);
                compensationService.CalculateCompensation(_records, _input.CalculationDate);
            }

            var vacationPayService = new VacationPayService();
            _vacationPayResults = _input.Vacations.Count > 0
                ? vacationPayService.Calculate(_input)
                : new List<VacationPayResult>();

            if (_input.CalculateUnusedVacationCompensation)
            {
                var dismissalDate = _input.DismissalDate ?? _input.CalculationDate;
                _unusedVacationCompensation = vacationPayService.CalculateUnusedVacationCompensation(_input, dismissalDate);
            }
            else
            {
                _unusedVacationCompensation = null;
            }

            DisplayResults();
            StatusLabel.Content = "Расчёт завершён";
            
            this.Loaded += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    AdjustWindowSize();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }
        catch (Exception ex)
        {
            var errorWindow = new ErrorWindow("Ошибка при расчёте", 
                $"Ошибка: {ex.Message}\n\nДетали:\n{ex}");
            errorWindow.Owner = this;
            errorWindow.ShowDialog();
            StatusLabel.Content = "Ошибка";
        }
    }

    private void DisplayResults()
    {
        DisplaySummary();
        DisplayPayments();
    }

    private void DisplaySummary()
    {
        var baseNet = Math.Round(_input.GrossSalary * 0.87m, 2);
        SalaryTextBlock.Text = $"{_input.GrossSalary:N2} / {baseNet:N2} руб. (gross / net)";

        IndexationRulesItemsControl.Items.Clear();
        if (_input.IndexationRules.Count > 0)
        {
            var sortedRules = _input.IndexationRules.OrderBy(r => r.Date).ToList();
            foreach (var rule in sortedRules)
            {
                var status = rule.IsPerformed ? "проведена" : "не проведена";
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                
                if (!rule.IsPerformed)
                {
                    var indexedGross = _input.GetIndexedGrossSalary(rule.Date);
                    var indexedNet = _input.GetIndexedNetSalary(rule.Date);
                var label = new TextBlock 
                { 
                    Text = $"Индексированный оклад на {rule.Date:dd.MM.yyyy} ({status}):",
                    Margin = new Thickness(0, 0, 10, 5),
                    FontSize = 13
                };
                var value = new TextBlock 
                { 
                    Text = $"{indexedGross:N2} / {indexedNet:N2} руб. (gross / net)",
                    Margin = new Thickness(0, 0, 0, 5),
                    FontSize = 13
                };
                    Grid.SetColumn(label, 0);
                    Grid.SetColumn(value, 1);
                    grid.Children.Add(label);
                    grid.Children.Add(value);
                }
                else
                {
                var label = new TextBlock 
                { 
                    Text = $"Индексация на {rule.Date:dd.MM.yyyy} ({status})",
                    Margin = new Thickness(0, 0, 0, 5),
                    FontSize = 13
                };
                    Grid.SetColumnSpan(label, 2);
                    grid.Children.Add(label);
                }
                IndexationRulesItemsControl.Items.Add(grid);
            }
        }

        var totalDiff = _records.Sum(x => x.Underpayment);
        var totalCompensation = _records.Sum(x => x.Compensation);
        
        TotalUnderpaymentTextBlock.Text = $"{totalDiff:N2} руб.";
        if (_input.CalculateIndexationUnderpayments)
        {
            DebtGroupBox.Header = "Результаты расчёта задолженности";
            TotalCompensationTextBlock.Text = $"{totalCompensation:N2} руб.";
            TotalToPayTextBlock.Text = $"{totalDiff + totalCompensation:N2} руб.";
        }
        else
        {
            DebtGroupBox.Header = "Отчёт по зарплате";
            TotalCompensationTextBlock.Text = "—";
            TotalToPayTextBlock.Text = "—";
        }

        var holidayWorkRecords = _records.Where(r => r.Type == PaymentType.HolidayWork).OrderBy(r => r.PaymentDate).ToList();
        var sickLeaveRecords = _records.Where(r => r.Type == PaymentType.SickLeave).OrderBy(r => r.PaymentDate).ToList();
        var vacationRecords = _records.Where(r => r.Type == PaymentType.Vacation).OrderBy(r => r.PaymentDate).ToList();

        HolidayWorkItemsControl.Items.Clear();
        if (holidayWorkRecords.Count > 0)
        {
            HolidayWorkGroupBox.Visibility = Visibility.Visible;
            foreach (var record in holidayWorkRecords)
            {
                var grid = CreatePaymentRecordGrid(record);
                HolidayWorkItemsControl.Items.Add(grid);
            }
        }
        else
        {
            HolidayWorkGroupBox.Visibility = Visibility.Collapsed;
        }

        SickLeaveItemsControl.Items.Clear();
        if (sickLeaveRecords.Count > 0)
        {
            SickLeaveGroupBox.Visibility = Visibility.Visible;
            foreach (var record in sickLeaveRecords)
            {
                var grid = CreatePaymentRecordGrid(record);
                SickLeaveItemsControl.Items.Add(grid);
            }
        }
        else
        {
            SickLeaveGroupBox.Visibility = Visibility.Collapsed;
        }

        VacationPaymentsItemsControl.Items.Clear();
        if (vacationRecords.Count > 0)
        {
            VacationPaymentsGroupBox.Visibility = Visibility.Visible;
            foreach (var record in vacationRecords)
            {
                var grid = CreatePaymentRecordGrid(record);
                VacationPaymentsItemsControl.Items.Add(grid);
            }
        }
        else
        {
            VacationPaymentsGroupBox.Visibility = Visibility.Collapsed;
        }

        DisplayOptionalCalculations();
        DisplayUnusedVacationCompensation();
    }

    private void DisplayOptionalCalculations()
    {
        var hasOptionalData = false;
        
        VacationPayItemsControl.Items.Clear();
        if (_vacationPayResults.Count > 0)
        {
            hasOptionalData = true;
            VacationPayGroupBox.Visibility = Visibility.Visible;
            foreach (var result in _vacationPayResults)
            {
                var groupBox = new GroupBox 
                { 
                    Header = $"Период: {result.From:dd.MM.yyyy} - {result.To:dd.MM.yyyy}",
                    Margin = new Thickness(0, 0, 0, 10),
                    FontSize = 13
                };
                var stackPanel = new StackPanel { Margin = new Thickness(10) };
                
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                int row = 0;
                AddGridRow(grid, row++, "Календарных дней:", result.CalendarDays.ToString());
                AddGridRow(grid, row++, "Средний дневной заработок (gross):", $"{result.AvgDailyGross:N2} руб.");
                AddGridRow(grid, row++, "Отпускные (gross):", $"{result.CalculatedGross:N2} руб.");
                AddGridRow(grid, row++, "Отпускные (net):", $"{result.CalculatedNet:N2} руб.");
                AddGridRow(grid, row++, "Выплачено (net):", $"{result.PaidNet:N2} руб.");
                if (result.DifferenceNet.HasValue)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    AddGridRow(grid, row++, "Недоплата (net):", $"{result.DifferenceNet.Value:N2} руб.");
                }
                
                stackPanel.Children.Add(grid);
                groupBox.Content = stackPanel;
                VacationPayItemsControl.Items.Add(groupBox);
            }
        }
        else
        {
            VacationPayGroupBox.Visibility = Visibility.Collapsed;
        }

        OptionalCalculationsTabItem.Visibility = hasOptionalData ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DisplayUnusedVacationCompensation()
    {
        if (_unusedVacationCompensation != null)
        {
            UnusedVacationGroupBox.Visibility = Visibility.Visible;
            UnusedVacationHireDateTextBlock.Text = _unusedVacationCompensation.HireDate.ToString("dd.MM.yyyy");
            var dismissalDate = _input.DismissalDate ?? _input.CalculationDate;
            UnusedVacationDismissalDateTextBlock.Text = dismissalDate.ToString("dd.MM.yyyy");
            
            UnusedVacationWorkMonthsTextBlock.Text = _unusedVacationCompensation.WorkMonths.ToString();
            UnusedVacationEarnedDaysTextBlock.Text = _unusedVacationCompensation.EarnedVacationDays.ToString();
            UnusedVacationUsedDaysTextBlock.Text = _unusedVacationCompensation.UsedVacationDays.ToString();
            UnusedVacationUnusedDaysTextBlock.Text = _unusedVacationCompensation.UnusedVacationDays.ToString();
            UnusedVacationAvgDailyWithoutTextBlock.Text = $"{_unusedVacationCompensation.AvgDailyGrossWithoutIndexation:N2} руб.";
            UnusedVacationCompensationWithoutTextBlock.Text = $"{_unusedVacationCompensation.CompensationNetWithoutIndexation:N2} руб.";
            UnusedVacationAvgDailyWithTextBlock.Text = $"{_unusedVacationCompensation.AvgDailyGross:N2} руб.";
            UnusedVacationCompensationWithTextBlock.Text = $"{_unusedVacationCompensation.CompensationNet:N2} руб.";
            UnusedVacationDifferenceTextBlock.Text = $"{_unusedVacationCompensation.DifferenceNet:N2} руб.";
        }
        else
        {
            UnusedVacationGroupBox.Visibility = Visibility.Collapsed;
        }
    }

    private Grid CreatePaymentRecordGrid(PaymentRecord record)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Margin = new Thickness(0, 0, 0, 10);
        
        int row = 0;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddGridRow(grid, row++, "Дата выплаты:", record.PaymentDate.ToString("dd.MM.yyyy"));
        
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddGridRow(grid, row++, "Выплачено (net):", $"{record.NetAmount:N2} руб.");
        
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddGridRow(grid, row++, "Должно быть выплачено (net):", $"{record.IndexedNetAmount:N2} руб.");
        
        if (record.Underpayment > 0)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddGridRow(grid, row++, "Недоплата (net):", $"{record.Underpayment:N2} руб.", 
                foreground: new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F")));
        }
        
        if (record.Compensation > 0)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddGridRow(grid, row++, "Компенсация (net):", $"{record.Compensation:N2} руб.", 
                foreground: new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F")));
        }
        
        return grid;
    }

    private void AddGridRow(Grid grid, int row, string label, string value, Brush? foreground = null)
    {
        var labelBlock = new TextBlock 
        { 
            Text = label,
            Margin = new Thickness(0, 0, 10, 5),
            FontSize = 13
        };
        var valueBlock = new TextBlock 
        { 
            Text = value,
            Margin = new Thickness(0, 0, 0, 5),
            FontSize = 13
        };
        if (foreground != null)
        {
            valueBlock.Foreground = foreground;
            valueBlock.FontWeight = FontWeights.SemiBold;
        }
        Grid.SetRow(labelBlock, row);
        Grid.SetColumn(labelBlock, 0);
        Grid.SetRow(valueBlock, row);
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
    }

    private List<PaymentGroup> _paymentGroups = new();
    private Dictionary<PaymentGroup, List<DataGridRow>> _groupRows = new();

    private void DisplayPayments()
    {
        _paymentGroups = _records
            .GroupBy(r => new { r.Year, r.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g => new PaymentGroup
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Payments = g.OrderBy(p => p.PaymentDate).ToList(),
                IsExpanded = false
            })
            .ToList();

        _groupRows.Clear();
        RefreshPaymentsGrid();
        
        if (_records.Count > 0)
        {
            var totalNet = _records.Sum(r => r.NetAmount);
            var totalNdfl = _records.Sum(r => r.NdflAmount);
            var totalIndexedNet = _records.Sum(r => r.IndexedNetAmount);
            var totalUnderpayment = _records.Sum(r => r.Underpayment);
            var totalCompensation = _records.Sum(r => r.Compensation);
            var totalToPay = totalUnderpayment + totalCompensation;
            
            TotalNetTextBlock.Text = totalNet.ToString("N2");
            TotalNdflTextBlock.Text = totalNdfl.ToString("N2");
            TotalIndexedNetTextBlock.Text = totalIndexedNet.ToString("N2");
            PaymentsTotalUnderpaymentTextBlock.Text = totalUnderpayment.ToString("N2");
            PaymentsTotalCompensationTextBlock.Text = totalCompensation.ToString("N2");
            PaymentsTotalToPayTextBlock.Text = totalToPay.ToString("N2");
        }
        else
        {
            TotalNetTextBlock.Text = "0.00";
            TotalNdflTextBlock.Text = "0.00";
            TotalIndexedNetTextBlock.Text = "0.00";
            PaymentsTotalUnderpaymentTextBlock.Text = "0.00";
            PaymentsTotalCompensationTextBlock.Text = "0.00";
            PaymentsTotalToPayTextBlock.Text = "0.00";
        }
    }

    private void RefreshPaymentsGrid()
    {
        var flatList = new List<object>();
        foreach (var group in _paymentGroups)
        {
            flatList.Add(group);
            if (group.IsExpanded)
            {
                foreach (var payment in group.Payments)
                {
                    flatList.Add(payment);
                }
            }
        }
        PaymentsDataGrid.ItemsSource = null;
        PaymentsDataGrid.ItemsSource = flatList;
    }

    private PaymentGroup? _expandingGroup;

    private void PaymentsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PaymentsDataGrid.SelectedItem is PaymentGroup group)
        {
            var wasExpanded = group.IsExpanded;
            group.IsExpanded = !group.IsExpanded;
            
            if (group.IsExpanded && !wasExpanded)
            {
                _expandingGroup = group;
                RefreshPaymentsGrid();
                PaymentsDataGrid.UpdateLayout();
                AnimateRowsExpansion(group);
            }
            else if (!group.IsExpanded && wasExpanded)
            {
                AnimateRowsCollapse(group);
            }
            
            PaymentsDataGrid.SelectedItem = null;
        }
    }

    private void AnimateRowsExpansion(PaymentGroup group)
    {
        PaymentsDataGrid.UpdateLayout();
        
        var groupRow = (DataGridRow)PaymentsDataGrid.ItemContainerGenerator.ContainerFromItem(group);
        if (groupRow != null)
        {
            PaymentsDataGrid.UpdateLayout();
            AnimateIconRotation(groupRow, true);
        }
        
        var startIndex = PaymentsDataGrid.Items.IndexOf(group) + 1;
        var rowCount = group.Payments.Count;
        
        for (int i = 0; i < rowCount; i++)
        {
            var rowIndex = startIndex + i;
            if (rowIndex < PaymentsDataGrid.Items.Count)
            {
                var row = (DataGridRow)PaymentsDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row != null && row.Item is PaymentRecord)
                {
                    row.Height = 0;
                    row.Visibility = Visibility.Visible;
                    row.Opacity = 0;
                    
                    var heightAnimation = new DoubleAnimation
                    {
                        From = 0,
                        To = 30,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    
                    var opacityAnimation = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                        BeginTime = TimeSpan.FromMilliseconds(50)
                    };
                    
                    row.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation);
                    row.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
                }
            }
        }
    }
    
    private void AnimateIconRotation(DataGridRow groupRow, bool expand)
    {
        var icon = FindVisualChild<TextBlock>(groupRow, "ExpandIcon");
        if (icon != null)
        {
            var transform = icon.RenderTransform as RotateTransform;
            if (transform == null)
            {
                transform = new RotateTransform();
                icon.RenderTransform = transform;
                icon.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            
            var currentAngle = transform.Angle;
            var targetAngle = expand ? 90 : 0;
            
            if (Math.Abs(currentAngle - targetAngle) < 0.1)
            {
                return;
            }
            
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
            transform.Angle = currentAngle;
            
            var animation = new DoubleAnimation
            {
                From = currentAngle,
                To = targetAngle,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            
            animation.Completed += (s, e) =>
            {
                transform.BeginAnimation(RotateTransform.AngleProperty, null);
                transform.Angle = targetAngle;
            };
            
            transform.BeginAnimation(RotateTransform.AngleProperty, animation);
        }
    }
    
    private void SetIconRotation(DataGridRow groupRow, bool expanded)
    {
        var icon = FindVisualChild<TextBlock>(groupRow, "ExpandIcon");
        if (icon != null)
        {
            var transform = icon.RenderTransform as RotateTransform;
            if (transform == null)
            {
                transform = new RotateTransform();
                icon.RenderTransform = transform;
                icon.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
            transform.Angle = expanded ? 90 : 0;
        }
    }
    
    private T? FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            if (child is T t && (child as FrameworkElement)?.Name == childName)
            {
                return t;
            }
            
            var childOfChild = FindVisualChild<T>(child, childName);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }

    private void AnimateRowsCollapse(PaymentGroup group)
    {
        PaymentsDataGrid.UpdateLayout();
        
        var groupRow = (DataGridRow)PaymentsDataGrid.ItemContainerGenerator.ContainerFromItem(group);
        if (groupRow != null)
        {
            PaymentsDataGrid.UpdateLayout();
            AnimateIconRotation(groupRow, false);
        }
        
        var startIndex = PaymentsDataGrid.Items.IndexOf(group) + 1;
        var rowCount = group.Payments.Count;
        var animatedRows = new List<DataGridRow>();
        var completedCount = 0;
        
        for (int i = 0; i < rowCount; i++)
        {
            var rowIndex = startIndex + i;
            if (rowIndex < PaymentsDataGrid.Items.Count)
            {
                var row = (DataGridRow)PaymentsDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row != null && row.Item is PaymentRecord)
                {
                    animatedRows.Add(row);
                    var currentHeight = row.ActualHeight > 0 ? row.ActualHeight : 30;
                    
                    var heightAnimation = new DoubleAnimation
                    {
                        From = currentHeight,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                    };
                    
                    var opacityAnimation = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(200))
                    };
                    
                    heightAnimation.Completed += (s, e) =>
                    {
                        completedCount++;
                        if (completedCount == animatedRows.Count)
                        {
                            RefreshPaymentsGrid();
                        }
                    };
                    
                    row.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation);
                    row.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
                }
            }
        }
        
        if (animatedRows.Count == 0)
        {
            RefreshPaymentsGrid();
        }
    }

    private void PaymentsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is PaymentGroup group)
        {
            e.Row.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 248, 255));
            e.Row.FontWeight = FontWeights.Bold;
            e.Row.MinHeight = 35;
            e.Row.Height = 35;
            
            PaymentsDataGrid.UpdateLayout();
            SetIconRotation(e.Row, group.IsExpanded);
        }
        else if (e.Row.Item is PaymentRecord)
        {
            e.Row.Background = System.Windows.Media.Brushes.White;
            e.Row.FontWeight = FontWeights.Normal;
            e.Row.MinHeight = 30;
            
            if (_expandingGroup != null && _expandingGroup.IsExpanded)
            {
                e.Row.Height = 0;
                e.Row.Visibility = Visibility.Visible;
            }
            else
            {
                e.Row.Height = 30;
            }
        }
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseDirectory = GetApplicationDirectory();
            var excelPath = Path.Combine(baseDirectory, "РасчетВыплатЗарплаты.xlsx");
            
            var excelService = new ExcelExportService();
            excelService.Export(_input, _records, _vacationPayResults, _unusedVacationCompensation, excelPath);
            
            MessageBox.Show($"Результаты сохранены в файл:\n{excelPath}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var errorWindow = new ErrorWindow("Ошибка при сохранении Excel", 
                $"Ошибка: {ex.Message}\n\nДетали:\n{ex}");
            errorWindow.Owner = this;
            errorWindow.ShowDialog();
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseDirectory = GetApplicationDirectory();
            var csvPath = Path.Combine(baseDirectory, "РасчетВыплатЗарплаты.csv");
            
            var csvService = new CsvExportService();
            csvService.Export(_input, _records, _vacationPayResults, _unusedVacationCompensation, csvPath);
            
            MessageBox.Show($"Результаты сохранены в файл:\n{csvPath}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var errorWindow = new ErrorWindow("Ошибка при сохранении CSV", 
                $"Ошибка: {ex.Message}\n\nДетали:\n{ex}");
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

    private void AdjustWindowSize()
    {
        this.UpdateLayout();
        
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        
        var windowChromeHeight = 40;
        var tabHeaderHeight = 40;
        var footerHeight = 60;
        var margins = 30;
        
        var rowHeight = 30;
        var groupRowHeight = 35;
        var headerRowHeight = 45;
        
        var totalRows = 0;
        foreach (var group in _paymentGroups)
        {
            totalRows++;
            if (group.IsExpanded)
            {
                totalRows += group.Payments.Count;
            }
        }
        
        var groupsCount = _paymentGroups.Count;
        var expandedRowsCount = totalRows - groupsCount;
        
        var calculatedHeight = windowChromeHeight + tabHeaderHeight + headerRowHeight + 
                               (groupsCount * groupRowHeight) + 
                               (expandedRowsCount * rowHeight) + 
                               footerHeight + margins;
        
        var maxHeight = screenHeight * 0.9;
        var minHeight = 600;
        var optimalHeight = Math.Max(minHeight, Math.Min(calculatedHeight, maxHeight));
        
        var optimalWidth = Math.Min(1400, screenWidth * 0.95);
        
        this.Height = optimalHeight;
        this.Width = optimalWidth;
        
        if (this.WindowState == WindowState.Normal)
        {
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }
}

public class TotalPaymentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PaymentRecord record)
        {
            var total = record.Underpayment + record.Compensation;
            return total.ToString("N2", culture);
        }
        if (value is PaymentGroup group)
        {
            var total = group.Underpayment + group.Compensation;
            return total.ToString("N2", culture);
        }
        return "0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ExpandIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? "▼" : "▶";
        }
        return "▶";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
