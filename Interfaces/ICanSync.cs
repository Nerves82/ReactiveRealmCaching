

namespace ReactiveRealmCaching.Interfaces
{

	public interface ICanSync : IHaveAnObjectId
	{
		bool ShouldSync { get; set; }
	}
}