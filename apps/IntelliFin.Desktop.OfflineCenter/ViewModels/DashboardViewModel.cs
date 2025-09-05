using IntelliFin.Desktop.OfflineCenter.Models;
using IntelliFin.Desktop.OfflineCenter.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace IntelliFin.Desktop.OfflineCenter.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly IOfflineDataService _offlineDataService;
    private readonly IFinancialApiService _financialApiService;
    
    private DashboardSummary _summary = new();
    private ObservableCollection<LoanSummary> _recentLoans = new();
    private ObservableCollection<OfflinePayment> _recentPayments = new();
    private bool _isOnline;

    public DashboardViewModel(IOfflineDataService offlineDataService, IFinancialApiService financialApiService)
    {
        _offlineDataService = offlineDataService;
        _financialApiService = financialApiService;
        
        Title = "Dashboard";
        
        RefreshCommand = new Command(async () => await ExecuteAsync(RefreshAsync));
        
        _ = Task.Run(LoadDataAsync);
    }

    public DashboardSummary Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public ObservableCollection<LoanSummary> RecentLoans
    {
        get => _recentLoans;
        set => SetProperty(ref _recentLoans, value);
    }

    public ObservableCollection<OfflinePayment> RecentPayments
    {
        get => _recentPayments;
        set => SetProperty(ref _recentPayments, value);
    }

    public bool IsOnline
    {
        get => _isOnline;
        set => SetProperty(ref _isOnline, value);
    }

    public ICommand RefreshCommand { get; }

    private async Task LoadDataAsync()
    {
        try
        {
            IsBusy = true;
            
            // Check connectivity
            IsOnline = await _financialApiService.CheckConnectivityAsync();
            
            // Load dashboard summary
            if (IsOnline)
            {
                try
                {
                    Summary = await _financialApiService.GetDashboardSummaryAsync();
                }
                catch
                {
                    // Fall back to offline data
                    Summary = await _offlineDataService.GetDashboardSummaryAsync();
                }
            }
            else
            {
                Summary = await _offlineDataService.GetDashboardSummaryAsync();
            }
            
            // Load recent loans
            var loanSummaries = await _offlineDataService.GetLoanSummariesAsync();
            var recentLoansList = loanSummaries.Take(10).ToList();
            
            RecentLoans.Clear();
            foreach (var loan in recentLoansList)
            {
                RecentLoans.Add(loan);
            }
            
            // Load recent payments
            var payments = await _offlineDataService.GetPaymentsAsync();
            var recentPaymentsList = payments.Take(10).ToList();
            
            RecentPayments.Clear();
            foreach (var payment in recentPaymentsList)
            {
                RecentPayments.Add(payment);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex, "loading dashboard data");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    public string FormatCurrency(decimal amount)
    {
        return $"ZMW {amount:N2}";
    }

    public string FormatPercentage(decimal percentage)
    {
        return $"{percentage:F1}%";
    }

    public string GetStatusColor(string status)
    {
        return status.ToLower() switch
        {
            "active" => "#10B981", // Green
            "overdue" => "#EF4444", // Red
            "current" => "#3B82F6", // Blue
            "completed" => "#10B981", // Green
            _ => "#6B7280" // Gray
        };
    }

    public string GetClassificationColor(string classification)
    {
        return classification.ToLower() switch
        {
            "normal" => "#10B981", // Green
            "special mention" => "#F59E0B", // Yellow
            "substandard" => "#EF4444", // Red
            "doubtful" => "#DC2626", // Dark Red
            "loss" => "#7F1D1D", // Very Dark Red
            _ => "#6B7280" // Gray
        };
    }
}
