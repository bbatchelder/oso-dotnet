using System.Collections.Generic;
using Xunit;

namespace Oso.Tests;

public class EnforcementTests
{
    private Oso _oso;
    public EnforcementTests()
    {
        _oso = new Oso();
        _oso.RegisterClass(typeof(User), "User");
        _oso.RegisterClass(typeof(Widget), "Widget");
    }
    public record class User(string Name);
    public record class Widget(int Id);
    public record class Request(string Method, string Path);
    [Fact]
    public void TestAuthorize()
    {
        User guest = new User("guest");
        User admin = new User("admin");
        Widget widget0 = new Widget(0);
        Widget widget1 = new Widget(1);

        _oso.LoadStr(@"
        allow(_actor: User, ""read"", widget: Widget) if
            widget.Id = 0;
        allow(actor: User, ""update"", _widget: Widget) if
            actor.Name = ""admin"";
    ");

        _oso.Authorize(guest, "read", widget0);
        _oso.Authorize(admin, "update", widget1);

        // Throws a forbidden exception when user can read resource
        Assert.Throws<ForbiddenException>(() => _oso.Authorize(guest, "update", widget0));

        // Throws a not found exception when user cannot read resource
        Assert.Throws<NotFoundException>(() => _oso.Authorize(guest, "read", widget1));
        Assert.Throws<NotFoundException>(() => _oso.Authorize(guest, "update", widget1));

        // With checkRead = false, returns a forbidden exception
        Assert.Throws<ForbiddenException>(() => _oso.Authorize(guest, "read", widget1, false));
        Assert.Throws<ForbiddenException>(() => _oso.Authorize(guest, "update", widget1, false));
    }

    [Fact]
    public void TestAuthorizeRequest()
    {
        _oso.RegisterClass(typeof(Request), "Request");
        _oso.LoadStr(@"
            allow_request(_: User{Name: ""guest""}, request: Request) if
                request.Path.StartsWith(""/repos"");
            allow_request(_: User{Name: ""verified""}, request: Request) if
                request.Path.StartsWith(""/account"");
        ");
        User guest = new User("guest");
        User verified = new User("verified");

        _oso.AuthorizeRequest(guest, new Request("GET", "/repos/1"));
        Assert.Throws<ForbiddenException>(() => _oso.AuthorizeRequest(guest, new Request("GET", "/other")));

        _oso.AuthorizeRequest(verified, new Request("GET", "/account"));
        Assert.Throws<ForbiddenException>(() => _oso.AuthorizeRequest(guest, new Request("GET", "/account")));
    }

    [Fact]
    public void TestAuthorizedActions()
    {
        _oso.LoadStr(@"
            allow(_actor: User{Name: ""sally""}, action, _resource: Widget{Id: 1})
                if action in [""CREATE"", ""READ""];
        ");

        User actor = new User("sally");
        Widget widget = new Widget(1);
        HashSet<object> actions = _oso.AuthorizedActions(actor, widget);

        var expected = new HashSet<object> { "CREATE", "READ" };
        Assert.Equal(expected, actions);

        _oso.ClearRules();

        _oso.LoadStr(@"
            allow(_actor: User{Name: ""fred""}, action, _resource: Widget{Id: 2})
                if action in [1, 2, 3, 4];
        ");

        User actor2 = new User("fred");
        Widget widget2 = new Widget(2);
        HashSet<object> actions2 = _oso.AuthorizedActions(actor2, widget2);

        var expected2 = new HashSet<object> { 1, 2, 3, 4 };
        Assert.Equal(expected2, actions2);

        User actor3 = new User("doug");
        Widget widget3 = new Widget(4);
        Assert.Empty(_oso.AuthorizedActions(actor3, widget3));
    }

    [Fact]
    public void TestAuthorizedActionsWildcard()
    {
        _oso.LoadStr("allow(_actor: User{Name: \"John\"}, _action, _resource: Widget{Id: 1});");

        User actor = new User("John");
        Widget widget = new Widget(1);

        var expected = new HashSet<object> { "*" };
        Assert.Equal(expected, _oso.AuthorizedActions(actor, widget, true));
        Assert.Throws<OsoException>(() => _oso.AuthorizedActions(actor, widget, false));
    }

    [Fact]
    public void TestAuthorizeField()
    {
        _oso.LoadStr(@"
            # Admins can update all fields
            allow_field(actor: User, ""update"", _widget: Widget, field) if
                actor.Name = ""admin"" and
                field in [""name"", ""purpose"", ""private_field""];
                
            # Anybody who can update a field can also read it
            allow_field(actor, ""read"", widget: Widget, field) if
                allow_field(actor, ""update"", widget, field);

            # Anybody can read public fields
            allow_field(_: User, ""read"", _: Widget, field) if
                field in [""name"", ""purpose""];
        ");
        User admin = new User("admin");
        User guest = new User("guest");
        Widget widget = new Widget(0);

        _oso.AuthorizeField(admin, "update", widget, "purpose");
        Assert.Throws<ForbiddenException>(() => _oso.AuthorizeField(admin, "update", widget, "foo"));

        _oso.AuthorizeField(guest, "read", widget, "purpose");
        Assert.Throws<ForbiddenException>(() => _oso.AuthorizeField(guest, "read", widget, "private_field"));
    }

    [Fact]
    public void TestAuthorizedFields()
    {
        _oso.LoadStr(@"
            # Admins can update all fields
            allow_field(actor: User, ""update"", _widget: Widget, field) if
                actor.Name = ""admin"" and
                field in [""name"", ""purpose"", ""private_field""];
                
            # Anybody who can update a field can also read it
            allow_field(actor, ""read"", widget: Widget, field) if
                allow_field(actor, ""update"", widget, field);

            # Anybody can read public fields
            allow_field(_: User, ""read"", _: Widget, field) if
                field in [""name"", ""purpose""];
        ");
        User admin = new User("admin");
        User guest = new User("guest");
        Widget widget = new Widget(0);

        // Admins should be able to update all fields
        var expected = new HashSet<object> { "name", "purpose", "private_field" };
        Assert.Equal(expected, _oso.AuthorizedFields(admin, "update", widget));

        // Admins should be able to read all fields
        var expected2 = new HashSet<object> { "name", "purpose", "private_field" };
        Assert.Equal(expected2, _oso.AuthorizedFields(admin, "read", widget));

        // Guests should not be able to update any fields
        var expected3 = new HashSet<object>();
        Assert.Equal(expected3, _oso.AuthorizedFields(guest, "update", widget));

        // Guests should be able to read public fields
        var expected4 = new HashSet<object> { "name", "purpose" };
        Assert.Equal(expected4, _oso.AuthorizedFields(guest, "read", widget));
    }

    [Fact]
    public void TestCustomReadAction()
    {
        _oso.ReadAction = "fetch";
        _oso.LoadStr("allow(\"graham\", \"fetch\", \"bar\");");
        Assert.Throws<NotFoundException>(() => _oso.Authorize("sam", "frob", "bar"));
        // A user who can "fetch" should get a ForbiddenException instead of a
        // NotFoundException
        Assert.Throws<ForbiddenException>(() => _oso.Authorize("graham", "frob", "bar"));
    }
}