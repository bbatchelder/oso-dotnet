using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace Oso.Tests;

public class OsoTests
{
    private readonly Oso _oso;
    private static readonly string TestPath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "..", "..", "..");

    public OsoTests()
    {
        _oso = new Oso();
        _oso.RegisterClass(typeof(User), "User");
        _oso.RegisterClass(typeof(Widget), "Widget");
        _oso.RegisterClass(typeof(Company), "Company");
        _oso.LoadFiles(Path.Join(TestPath, "Resources", "test_oso.polar"));
    }

    public class User
    {
        public string Name { get; set; }

        public User(string name)
        {
            this.Name = name;
        }

        public List<Company> Companies() => new() { new Company(1) };
    }

    public class Widget
    {
        public int Id { get; }

        public Widget(int id)
        {
            this.Id = id;
        }
    }

    public class Company
    {
        public int Id { get; }

        public Company(int id)
        {
            this.Id = id;
        }

        public string Role(User a) => a.Name == "president" ? "admin" : "guest";

        public override bool Equals(object? obj) => obj is Company c && c.Id == this.Id;

        public override int GetHashCode() => this.Id;
    }

    [Fact]
    public void TestIsAllowed()
    {
        User guest = new User("guest");
        Widget resource1 = new Widget(1);
        Assert.True(_oso.IsAllowed(guest, "get", resource1));

        User president = new User("president");
        Company company = new Company(1);
        Assert.True(_oso.IsAllowed(president, "create", company));
    }

    [Fact]
    public void TestInstanceFromExternalCall()
    {
        Company company = new Company(1);
        User guest = new User("guest");
        Assert.True(_oso.IsAllowed(guest, "frob", company));

        // if the guest user can do it, then the dict should
        // create an instance of the user and be allowed
        Dictionary<string, string> userMap = new Dictionary<string, string>() { { "username", "guest" }};
        Assert.True(_oso.IsAllowed(userMap, "frob", company));
    }

    [Fact]
    public void TestAllowModel()
    {
        User auditor = new User("auditor");

        Assert.True(_oso.IsAllowed(auditor, "list", typeof(Company)));
        Assert.False(_oso.IsAllowed(auditor, "list", typeof(Widget)));
    }

    [Fact]
    public void TestGetAllowedActions()
    {

        Oso o = new Oso();
        o.RegisterClass(typeof(User), "User");
        o.RegisterClass(typeof(Widget), "Widget");

        o.LoadStr(@"
            allow(_actor: User{Name: ""sally""}, action, _resource: Widget{Id: 1})
                if action in [""CREATE"", ""READ""];
        ");

        User actor = new User("sally");
        Widget widget = new Widget(1);
        HashSet<object> actions = o.GetAllowedActions(actor, widget);

        var expected = new HashSet<object> { "CREATE", "READ" };
        Assert.Equal(expected, actions);

        o.ClearRules();

        o.LoadStr(
            "allow(_actor: User{Name: \"fred\"}, action, _resource: Widget{Id: 2})"
                + " if action in [1, 2, 3, 4];");

        User actor2 = new User("fred");
        Widget widget2 = new Widget(2);
        HashSet<object> actions2 = o.GetAllowedActions(actor2, widget2);
        var expected2 = new HashSet<object> { 1, 2, 3, 4 };
        Assert.Equal(expected2, actions2);

        User actor3 = new User("doug");
        Widget widget3 = new Widget(4);
        Assert.Empty(o.GetAllowedActions(actor3, widget3));
    }

    [Fact]
    public void TestAuthorizedActionsWildcard()
    {
        Oso o = new Oso();

        o.RegisterClass(typeof(User), "User");
        o.RegisterClass(typeof(Widget), "Widget");

        o.LoadStr("allow(_actor: User{Name: \"John\"}, _action, _resource: Widget{Id: 1});");

        User actor = new User("John");
        Widget widget = new Widget(1);

       var expected = new HashSet<object> { "*" };
        Assert.Equal(expected, o.AuthorizedActions(actor, widget, true));
        Assert.Throws<OsoException>(() => o.AuthorizedActions(actor, widget, false));
    }

    [Fact]
    public void TestNotEqualOperator()
    {
        Oso oso = new Oso();
        oso.RegisterClass(typeof(User), "User");
        oso.LoadStr("allow(actor: User, _action, _resource) if actor != nil;");
        Assert.False(oso.IsAllowed(null, "foo", "foo"));
    }
}