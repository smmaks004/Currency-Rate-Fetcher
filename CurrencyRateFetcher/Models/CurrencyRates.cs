using System;
using System.Collections.Generic;

namespace CurrencyRateFetcher.Models;

public partial class CurrencyRates
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public int CurrencyId { get; set; }

    public decimal ExchangeRate { get; set; }

    public virtual Currency Currency { get; set; } = null!;
}
