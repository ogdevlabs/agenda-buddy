namespace Library.Dtos;

public interface IUnit
{
    public int Value { get; set; }
    public string ShortDescription { get; }
    public string LongDescription { get; }
}