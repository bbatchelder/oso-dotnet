using Xunit;

namespace Oso.Tests;
public class HostTests
{
    public class User { }

    public class UserSubclass : User { }

    public class NotSubclass { }

    [Fact]
    public void IsSubclass()
    {
        var polar = new Polar();
        var host = polar.Host;
        host.CacheClass(typeof(User), "User");
        host.CacheClass(typeof(UserSubclass), "UserSubclass");
        host.CacheClass(typeof(NotSubclass), "NotSubclass");

        Assert.True(host.IsSubclass("UserSubclass", "User"));
        Assert.True(host.IsSubclass("UserSubclass", "UserSubclass"));
        Assert.True(host.IsSubclass("User", "User"));
        Assert.False(host.IsSubclass("User", "NotSubclass"));
        Assert.False(host.IsSubclass("User", "UserSubclass"));
    }
}