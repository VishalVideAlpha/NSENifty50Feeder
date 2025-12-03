using System.ComponentModel.DataAnnotations;

namespace CQGAPI.Models;

public class DBRate
{
    [Key]
    public string Key { get; set; } = string.Empty;
    public string Symbol { get; set; }=string.Empty;
    public double Bid { get; set; }
    public double Ask { get; set; }
    public double Ltp { get; set; }
    public double Open { get; set; }
    public double Close { get; set; }
    public double Low { get; set; }
    public double High { get; set; }
}
