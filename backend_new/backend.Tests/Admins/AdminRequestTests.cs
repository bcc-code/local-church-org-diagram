using Backend.Admins;
using JasperFx.CommandLine;
using Shouldly;

namespace Backend.Tests.Admins;

public class AdminRequestTests
{
    private const string Uid = "11111111-1111-1111-1111-111111111111";
    private static readonly Guid Person = new(Uid);

    /// <summary>Parses a command line as JasperFx does, then validates it.</summary>
    private static AdminRequest Parse(params string[] args)
    {
        var factory = new CommandFactory();
        factory.RegisterCommand<AdminsCommand>();
        var run = factory.BuildRun([AdminsCommand.Name, .. args]);
        run.Command.ShouldBeOfType<AdminsCommand>();
        return AdminRequest.From(run.Input.ShouldBeOfType<AdminsInput>());
    }

    private static AdminException Invalid(params string[] args)
    {
        var e = Should.Throw<AdminException>(() => Parse(args));
        e.Code.ShouldBe(AdminErrors.InvalidInput);
        return e;
    }

    [Fact]
    public void Add()
    {
        Parse("add", Uid, "--tenant", "5").ShouldBe(new AddAdmin(Person, 5, null, false));
        Parse("ADD", Uid, "--global", "--name", "  Kari Nordmann ", "--dry-run")
            .ShouldBe(new AddAdmin(Person, null, "Kari Nordmann", true));
    }

    [Fact]
    public void List()
    {
        Parse("list").ShouldBe(new ListAdmins(null, null, false));
        Parse("list", Uid, "--tenant", "5", "--global").ShouldBe(new ListAdmins(Person, 5, true));
    }

    [Fact]
    public void Remove()
    {
        Parse("remove", Uid, "--tenant", "5").ShouldBe(new RemoveAdmin(Person, new AdminScope(5), false, false, false));
        Parse("remove", Uid, "--global", "--yes", "--allow-last-global")
            .ShouldBe(new RemoveAdmin(Person, new AdminScope(null), false, true, true));
        Parse("remove", Uid, "--all", "--dry-run").ShouldBe(new RemoveAdmin(Person, null, true, false, false));
    }

    [Fact]
    public void Search()
    {
        Parse("search", " Kari ").ShouldBe(new SearchPersons("Kari", AdminRequest.DefaultSearchLimit));
        Parse("search", "Kari", "--limit", "3").ShouldBe(new SearchPersons("Kari", 3));
    }

    [Fact]
    public void UnknownAction_IsInvalid() =>
        Invalid("delete", Uid).Message.ShouldBe("Unknown action 'delete': use add, list, remove or search");

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    public void PersonUid_IsRequired(string action) =>
        Invalid(action, "--global").Message.ShouldContain("needs the person's UID");

    [Theory]
    [InlineData("Kari Nordmann")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void PersonUid_MustBeAUid(string person) =>
        Invalid("add", person, "--global").Message.ShouldContain("isn't a person UID");

    [Theory]
    [InlineData("add", Uid)]
    [InlineData("add", Uid, "--tenant", "5", "--global")]
    [InlineData("remove", Uid)]
    [InlineData("remove", Uid, "--tenant", "5", "--global")]
    [InlineData("remove", Uid, "--all", "--global")]
    [InlineData("remove", Uid, "--all", "--tenant", "5")]
    public void Scope_MustBeExactlyOne(params string[] args) => Invalid(args).Message.ShouldStartWith("Give one of");

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    public void Tenant_MustBePositive(string tenant) =>
        Invalid("add", Uid, "--tenant", tenant).Message.ShouldContain("positive BCC church ID");

    [Theory]
    [InlineData("add", Uid, "--global", "--yes")]
    [InlineData("add", Uid, "--global", "--all")]
    [InlineData("list", "--dry-run")]
    [InlineData("list", "--name", "x")]
    [InlineData("remove", Uid, "--global", "--name", "x")]
    [InlineData("search", "Kari", "--global")]
    [InlineData("search", "Kari", "--tenant", "5")]
    public void FlagsThatDontApply_AreInvalid(params string[] args) =>
        Invalid(args).Message.ShouldContain($"can't be used with {args[0]}");

    [Fact]
    public void Name_IsTrimmedAndAtMost200Characters()
    {
        Invalid("add", Uid, "--global", "--name", "   ").Message.ShouldBe("The name can't be empty");
        Invalid("add", Uid, "--global", "--name", new string('x', 201)).Message.ShouldContain("200");
        Parse("add", Uid, "--global", "--name", new string('x', 200)).ShouldBeOfType<AddAdmin>().Name!.Length.ShouldBe(200);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    public void SearchLimit_IsBetween1And100(string limit) =>
        Invalid("search", "Kari", "--limit", limit).Message.ShouldContain("between 1 and 100");

    [Fact]
    public void Search_NeedsText() => Invalid("search", "  ").Message.ShouldContain("search needs");
}
