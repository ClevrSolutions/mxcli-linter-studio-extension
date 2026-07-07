using Mendix.StudioPro.ExtensionsAPI.Model;
using Mendix.StudioPro.ExtensionsAPI.Model.DomainModels;
using Mendix.StudioPro.ExtensionsAPI.Model.Projects;
using NSubstitute;
using Xunit;

namespace Clevr.Lint.Extension.Tests;

public class NavigationCoordinatorTests
{
    [Fact]
    public void Resolve_NoModelOpen_ReturnsNoModel()
    {
        var coordinator = new NavigationCoordinator(() => null);

        var result = coordinator.Resolve("guid-1", "Module.Thing", "Microflow");

        Assert.Equal(NavigationRoute.NoModel, result.Route);
        Assert.Null(result.Unit);
    }

    [Theory]
    [InlineData("ProjectSecurity")]
    [InlineData("Security")]
    [InlineData("security")]
    public void Resolve_ProjectSecurityDocumentType_ReturnsProjectSecurity(string documentType)
    {
        var model = Substitute.For<IModel>();
        var coordinator = new NavigationCoordinator(() => model);

        var result = coordinator.Resolve(null, "Module.Anything", documentType);

        Assert.Equal(NavigationRoute.ProjectSecurity, result.Route);
    }

    [Fact]
    public void Resolve_GuidHit_ReturnsOpenedViaGuidRoute()
    {
        var model = Substitute.For<IModel>();
        var unit = Substitute.For<IAbstractUnit>();
        model.TryGetAbstractUnitById("guid-1", out Arg.Any<IAbstractUnit?>())
            .Returns(x =>
            {
                x[1] = unit;
                return true;
            });
        var coordinator = new NavigationCoordinator(() => model);

        var result = coordinator.Resolve("guid-1", "Module.Thing", "Microflow");

        Assert.Equal(NavigationRoute.Opened, result.Route);
        Assert.Same(unit, result.Unit);
        Assert.Contains("GUID-OK", result.Reason);
    }

    [Fact]
    public void Resolve_GuidMiss_FallsBackToNameRouteAndFindsDocument()
    {
        var doc = Substitute.For<IDocument>();
        doc.Name.Returns("MyMicroflow");

        var module = Substitute.For<IModule>();
        module.Name.Returns("MyModule");
        module.GetDocuments().Returns(new[] { doc });
        module.GetFolders().Returns(Array.Empty<IFolder>());

        var root = Substitute.For<IProject>();
        root.GetModules().Returns(new[] { module });

        var model = Substitute.For<IModel>();
        model.Root.Returns(root);
        model.TryGetAbstractUnitById(Arg.Any<string>(), out Arg.Any<IAbstractUnit?>()).Returns(false);

        var coordinator = new NavigationCoordinator(() => model);

        var result = coordinator.Resolve("missing-guid", "MyModule.MyMicroflow", "Microflow");

        Assert.Equal(NavigationRoute.Opened, result.Route);
        Assert.Same(doc, result.Unit);
        Assert.Null(result.Focus);
        Assert.Contains("GUID-MISS", result.Reason);
        Assert.Contains("route=NAME document", result.Reason);
    }

    [Fact]
    public void Resolve_EntityType_FocusesEntityInDomainModel()
    {
        var entity = Substitute.For<IEntity>();
        entity.Name.Returns("Customer");

        var domainModel = Substitute.For<IDomainModel>();
        domainModel.GetEntities().Returns(new[] { entity });

        var module = Substitute.For<IModule>();
        module.Name.Returns("Sales");
        module.DomainModel.Returns(domainModel);

        var root = Substitute.For<IProject>();
        root.GetModules().Returns(new[] { module });

        var model = Substitute.For<IModel>();
        model.Root.Returns(root);
        model.TryGetAbstractUnitById(Arg.Any<string>(), out Arg.Any<IAbstractUnit?>()).Returns(false);

        var coordinator = new NavigationCoordinator(() => model);

        var result = coordinator.Resolve(null, "Sales.Customer", "Entity");

        Assert.Equal(NavigationRoute.Opened, result.Route);
        Assert.Same(domainModel, result.Unit);
        Assert.Same(entity, result.Focus);
    }

    [Fact]
    public void Resolve_ModuleNotFound_ReturnsNotFound()
    {
        var root = Substitute.For<IProject>();
        root.GetModules().Returns(Array.Empty<IModule>());

        var model = Substitute.For<IModel>();
        model.Root.Returns(root);
        model.TryGetAbstractUnitById(Arg.Any<string>(), out Arg.Any<IAbstractUnit?>()).Returns(false);

        var coordinator = new NavigationCoordinator(() => model);

        var result = coordinator.Resolve(null, "Ghost.Thing", "Microflow");

        Assert.Equal(NavigationRoute.NotFound, result.Route);
    }

    [Fact]
    public void Resolve_SnippetTypeWithNoGuid_ReturnsSnippet()
    {
        var root = Substitute.For<IProject>();
        root.GetModules().Returns(Array.Empty<IModule>());

        var model = Substitute.For<IModel>();
        model.Root.Returns(root);
        model.TryGetAbstractUnitById(Arg.Any<string>(), out Arg.Any<IAbstractUnit?>()).Returns(false);

        var coordinator = new NavigationCoordinator(() => model);

        var result = coordinator.Resolve(null, "MyModule.MySnippet", "Snippet");

        Assert.Equal(NavigationRoute.Snippet, result.Route);
    }

    [Fact]
    public void Resolve_QualifiedNameWithoutModuleSeparator_ReturnsNotFound()
    {
        var model = Substitute.For<IModel>();
        model.TryGetAbstractUnitById(Arg.Any<string>(), out Arg.Any<IAbstractUnit?>()).Returns(false);

        var coordinator = new NavigationCoordinator(() => model);

        var result = coordinator.Resolve(null, "NoDotHere", "Microflow");

        Assert.Equal(NavigationRoute.NotFound, result.Route);
    }

    [Fact]
    public void Resolve_EnumerationType_SetsIsEnumeration()
    {
        var doc = Substitute.For<IDocument>();
        doc.Name.Returns("Status");

        var module = Substitute.For<IModule>();
        module.Name.Returns("MyModule");
        module.GetDocuments().Returns(new[] { doc });
        module.GetFolders().Returns(Array.Empty<IFolder>());

        var root = Substitute.For<IProject>();
        root.GetModules().Returns(new[] { module });

        var model = Substitute.For<IModel>();
        model.Root.Returns(root);
        model.TryGetAbstractUnitById(Arg.Any<string>(), out Arg.Any<IAbstractUnit?>()).Returns(false);

        var coordinator = new NavigationCoordinator(() => model);

        var result = coordinator.Resolve(null, "MyModule.Status", "Enumeration");

        Assert.True(result.IsEnumeration);
    }
}
