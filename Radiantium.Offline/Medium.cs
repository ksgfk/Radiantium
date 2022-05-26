namespace Radiantium.Offline
{
    public class MediumAdapter
    {
        public Medium? Inside { get; set; }
        public Medium? Outside { get; set; }

        public bool HasMedium => Inside != null || Outside != null;
        public bool HasOutsideMedium => Outside != null;
        public bool HasInsideMedium => Inside != null;

        public MediumAdapter(Medium? inside, Medium? outside)
        {
            Inside = inside;
            Outside = outside;
        }

        public MediumAdapter(Medium? same) : this(same, same) { }
    }

    public class Medium
    {
    }
}
