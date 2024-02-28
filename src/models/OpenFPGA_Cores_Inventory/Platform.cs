namespace Pannella.Models.OpenFPGA_Cores_Inventory;

public class Platform
{
    public string category { get; set; }
    public string name { get; set; }
    public int year { get; set; }
    public string manufacturer { get; set; }

    public override string ToString()
    {
        return this.name;
    }
}
