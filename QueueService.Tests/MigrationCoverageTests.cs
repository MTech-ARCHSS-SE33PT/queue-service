using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Migrations;
using QueueService.Infrastructure;
using QueueService.Migrations;

namespace QueueService.Tests;

public sealed class MigrationCoverageTests
{
    [Fact]
    public void Migrations_CanBuildUpDownOperationsAndTargetModels()
    {
        ExerciseMigration(new InitialCreate());
    }

    [Fact]
    public void Snapshot_CanBuildModel()
    {
        ExerciseSnapshot(typeof(QueueDbContext).Assembly.GetType(
            "QueueService.Migrations.QueueDbContextModelSnapshot",
            throwOnError: true)!);
    }

    private static void ExerciseMigration(Migration migration)
    {
        var up = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeNonPublic(migration, "Up", up);
        Assert.NotEmpty(up.Operations);

        var down = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeNonPublic(migration, "Down", down);
        Assert.NotEmpty(down.Operations);

        var modelBuilder = CreateModelBuilder();
        InvokeNonPublic(migration, "BuildTargetModel", modelBuilder);
        Assert.NotEmpty(modelBuilder.Model.GetEntityTypes());
    }

    private static void ExerciseSnapshot(Type snapshotType)
    {
        var snapshot = Activator.CreateInstance(snapshotType, nonPublic: true)!;
        var modelBuilder = CreateModelBuilder();
        InvokeNonPublic(snapshot, "BuildModel", modelBuilder);
        Assert.NotEmpty(modelBuilder.Model.GetEntityTypes());
    }

    private static ModelBuilder CreateModelBuilder()
        => new(new ConventionSet());

    private static void InvokeNonPublic(object instance, string methodName, object argument)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, new[] { argument });
    }
}
