using System.Text;
using LBElectronica.Server.Models;

namespace LBElectronica.Server.Services;

public class ReceiptService
{
    public string BuildReceiptText(Sale sale)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LB Electronica");
        sb.AppendLine($"Ticket: {sale.TicketNumber}");
        sb.AppendLine($"Fecha: {sale.Date:yyyy-MM-dd HH:mm}");
        sb.AppendLine("------------------------------");
        foreach (var item in sale.Items)
        {
            sb.AppendLine($"{item.Product?.Name}");
            sb.AppendLine($"{item.Qty} x {item.UnitPrice:0.00} = {(item.Qty * item.UnitPrice) - item.Discount:0.00}");
        }
        sb.AppendLine("------------------------------");
        sb.AppendLine($"Subtotal: {sale.Subtotal:0.00}");
        sb.AppendLine($"Descuento: {sale.DiscountTotal:0.00}");
        sb.AppendLine($"Total: {sale.Total:0.00}");
        sb.AppendLine($"Pago: {sale.PaymentMethod}");
        sb.AppendLine("Gracias por su compra");
        return sb.ToString();
    }
}
