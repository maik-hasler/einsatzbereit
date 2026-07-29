namespace Application.Common.Caching;

public interface ICacheInvalidator
{
	void Invalidate(string category);
}
