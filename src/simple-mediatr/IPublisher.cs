namespace simple_mediatr;

public interface IPublisher
{
    void Publish<T>(T notification);
}
