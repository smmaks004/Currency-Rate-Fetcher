using System;
using System.Collections.Generic;

namespace CurrencyRateFetcher.Models;

public partial class Currency
{
    public int Id { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public virtual ICollection<CurrencyRates> CurrencyRates { get; set; } = new List<CurrencyRates>();
}
