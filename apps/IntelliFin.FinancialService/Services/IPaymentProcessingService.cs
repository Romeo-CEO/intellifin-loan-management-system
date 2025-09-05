using IntelliFin.FinancialService.Models;

namespace IntelliFin.FinancialService.Services;

public interface IPaymentProcessingService
{
    Task<PaymentProcessingResult> ProcessPaymentAsync(ProcessPaymentRequest request);
    Task<PaymentReconciliationResult> ReconcilePaymentAsync(string paymentId);
    Task<IEnumerable<Payment>> GetPaymentHistoryAsync(string loanId);
    Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentId);
    Task<RefundResult> ProcessRefundAsync(ProcessRefundRequest request);
    Task<bool> ValidatePaymentMethodAsync(PaymentMethod paymentMethod, decimal amount);
    Task<TinggPaymentResult> ProcessTinggPaymentAsync(TinggPaymentRequest request);
    Task<PaymentGatewayHealthResult> CheckPaymentGatewayHealthAsync();
}
