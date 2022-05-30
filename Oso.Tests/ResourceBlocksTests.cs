using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace Oso.Tests;

public class ResourceBlocksTests
{

    private Polar _polar;
    private static readonly string TestPath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "..", "..", "..");
    public ResourceBlocksTests()
    {
      _polar = new Polar();
      _polar.RegisterClass(typeof(User), "User");
      _polar.RegisterClass(typeof(Role), "Role");
      _polar.RegisterClass(typeof(Repo), "Repo");
      _polar.RegisterClass(typeof(Org), "Org");
      _polar.RegisterClass(typeof(Issue), "Issue");
      _polar.RegisterClass(typeof(OrgRole), "OrgRole");
      _polar.RegisterClass(typeof(RepoRole), "RepoRole");
      var policyPath = Path.Join(TestPath, "Resources", "roles_policy.polar");
      _polar.LoadFiles(policyPath);

    }
    public record class Org(string Name);
    public record class Repo(string Name, Org Org);
    public record class Issue(string Name, Repo Repo);

    public abstract record class Role(string Name, object Resource);

    public record class OrgRole : Role
    {
        public OrgRole(string Name, Org Resource) : base(Name, Resource) { }
    }

    public record class RepoRole : Role
    {
        public RepoRole(string Name, Repo Resource) : base(Name, Resource) { }
    }

    public record class User(string Name, List<Role> Roles);

  [Fact]
  public void testResourceBlocks()
  {
    Org osohq = new Org("osohq"), apple = new Org("apple");
    Repo oso = new Repo("oso", osohq), ios = new Repo("ios", apple);
    Issue bug = new Issue("bug", oso), laggy = new Issue("laggy", ios);
    Role osohqOwner = new OrgRole("owner", osohq);
    Role osohqMember = new OrgRole("member", osohq);

    List<Role> osohqOwnerList = new() { osohqOwner };
    List<Role> osohqMemberList = new() { osohqMember };
    User leina = new User("leina", osohqOwnerList), steve = new User("steve", osohqMemberList);

    Assert.NotEmpty(_polar.QueryRule("allow", leina, "invite", osohq).Results);
    Assert.NotEmpty(_polar.QueryRule("allow", leina, "create_repo", osohq).Results);
    Assert.NotEmpty(_polar.QueryRule("allow", leina, "push", oso).Results);
    Assert.NotEmpty(_polar.QueryRule("allow", leina, "pull", oso).Results);
    Assert.NotEmpty(_polar.QueryRule("allow", leina, "edit", bug).Results);

    Assert.Empty(_polar.QueryRule("allow", steve, "invite", osohq).Results);
    Assert.NotEmpty(_polar.QueryRule("allow", steve, "create_repo", osohq).Results);
    Assert.Empty(_polar.QueryRule("allow", steve, "push", oso).Results);
    Assert.NotEmpty(_polar.QueryRule("allow", steve, "pull", oso).Results);
    Assert.Empty(_polar.QueryRule("allow", steve, "edit", bug).Results);

    Assert.Empty(_polar.QueryRule("allow", leina, "edit", laggy).Results);
    Assert.Empty(_polar.QueryRule("allow", steve, "edit", laggy).Results);

    User gabe = new User("gabe", new());
    Assert.Empty(_polar.QueryRule("allow", gabe, "edit", bug).Results);
    gabe = new User("gabe", osohqMemberList);
    Assert.Empty(_polar.QueryRule("allow", gabe, "edit", bug).Results);
    gabe = new User("gabe", osohqOwnerList);
    Assert.NotEmpty(_polar.QueryRule("allow", gabe, "edit", bug).Results);
  }
}