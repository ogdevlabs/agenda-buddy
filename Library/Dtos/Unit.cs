namespace Library.Dtos;

public class Unit :IUnit
{
    public int Value { get; set; }
    public string ShortDescription => "unit";

    public string LongDescription => "Unit of Measure";

    public override string ToString()
    {
        return this.Value + " " + this.ShortDescription;
    }

    public string ToString(UnitOfMeasureFormat format)
    {
        if (format == UnitOfMeasureFormat.Short)
        {
            return this.ToString();
        }

        return this.Value + " " + this.LongDescription;
    }
}

public enum UnitOfMeasureFormat { Short, Long }