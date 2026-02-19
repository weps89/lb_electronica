using LBElectronica.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Services;

public class ExchangeRateService(AppDbContext db)
{
    public async Task<decimal> GetCurrentRateAsync()
    {
        var rate = await db.ExchangeRates.OrderByDescending(x => x.EffectiveDate).Select(x => (decimal?)x.ArsPerUsd).FirstOrDefaultAsync();
        return rate is null or <= 0 ? 1m : rate.Value;
    }
}
