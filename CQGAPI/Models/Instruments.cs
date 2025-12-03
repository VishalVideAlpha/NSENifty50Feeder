namespace CQGAPI.Models;

public class Instruments
{
    public double divider { get; set; }
    public double tickSize { get; set; }
    public decimal ltp { get; set; }
    public decimal askSpotRate { get; set; }
    public decimal bidLotSize { get; set; }
    public string expiryDate { get; set; } = string.Empty;
    public decimal askMultiplier { get; set; }
    public string tradingToDayTime { get; set; } = string.Empty;
    public bool deleted { get; set; }
    public decimal maxUnit { get; set; }
    public string mName { get; set; } = string.Empty;
    public string symbol { get; set; } = string.Empty;

    public string symbolId { get; set; } = string.Empty;
    public string crossPnlPair { get; set; } = string.Empty;
    public decimal close { get; set; }
    public decimal high { get; set; }
    public string brokerageType { get; set; } = string.Empty;
    public decimal bidMultiplier { get; set; }
    public decimal minUnit { get; set; }
    public List<string>? closingTimes { get; set; }
    public string shareClassId { get; set; } = string.Empty;
    public string mSymbol { get; set; } = string.Empty;
    public decimal open { get; set; }
    public string tradingFromDayTime { get; set; } = string.Empty;
    public string symbolLogo { get; set; } = string.Empty;
    public int brokerageAmount { get; set; }
    public string tag { get; set; } = string.Empty;
    public decimal leverage { get; set; }
    public string mMarket { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string isinNumber { get; set; } = string.Empty;
    public decimal bidSpotRate { get; set; }
    public DateTime updatedAt { get; set; }
    public decimal askLotSize { get; set; }
    public string crossPnlType { get; set; } = string.Empty;
    public decimal idealDuration { get; set; }
    public int ltpChangePercentage { get; set; }
    public string adminUid { get; set; } = string.Empty;
    public string exchangeId { get; set; } = string.Empty;
    public DateTime timeStamp { get; set; }
    public decimal digitsAfterDecimal { get; set; }
    public decimal low { get; set; }
    public decimal minAskSize { get; set; }
    public decimal minBidSize { get; set; }
    public decimal ltpChange { get; set; }
    public string market { get; set; } = string.Empty;
    public decimal maxAskSize { get; set; }
    public decimal maxBidSize { get; set; }
    public decimal setOffDifference { get; set; }
    public string settlingCurrency { get; set; } = string.Empty;
    public decimal markup { get; set; }
}
