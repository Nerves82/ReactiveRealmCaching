namespace ReactiveRealmCaching.Interfaces;

public interface ICanBeDeleted
{
	bool IsMarkedForDeletion { get; set; }
}