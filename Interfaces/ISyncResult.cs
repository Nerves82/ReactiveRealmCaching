using System;

namespace ReactiveRealmCaching.Interfaces
{
	public interface ISyncResult<T>
	{
		bool DidError { get; set; }
		Exception? Exception { get; set; }
		T Object { get; set; }
	}
}