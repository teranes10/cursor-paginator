using Microsoft.CodeAnalysis.Text;

namespace CursorPaginator.Generator;

[Generator]
public sealed class QuerySourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var globalOptions = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
        {
            provider.GlobalOptions.TryGetValue("build_property.CursorPaginatorDatabaseProvider", out var dbProviderStr);
            provider.GlobalOptions.TryGetValue("build_property.CursorPaginatorNamingConvention", out var namingStr);

            var dbProvider = DatabaseProvider.MySQL;
            if (!string.IsNullOrWhiteSpace(dbProviderStr) && Enum.TryParse<DatabaseProvider>(dbProviderStr, true, out var parsedDb))
            {
                dbProvider = parsedDb;
            }

            var naming = NamingConvention.SnakeCase;
            if (!string.IsNullOrWhiteSpace(namingStr) && Enum.TryParse<NamingConvention>(namingStr, true, out var parsedNaming))
            {
                naming = parsedNaming;
            }

            return (DatabaseProvider: dbProvider, NamingConvention: naming);
        });

        // Register syntax provider
        var entities = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "CursorPaginator.Core.GenerateQueryAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, ct) => ctx.TargetSymbol as INamedTypeSymbol)
            .Combine(globalOptions)
            .Select(static (combined, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var (classSymbol, globalConfig) = combined;
                if (classSymbol is null)
                    return (Entity: null, Diagnostic: null);

                return EntityInfo.From(classSymbol, globalConfig.DatabaseProvider, globalConfig.NamingConvention);
            })
            .Where(static t => t.Entity is not null || t.Diagnostic is not null);

        // Register source output
        context.RegisterSourceOutput(entities, static (spc, t) =>
        {
            if (t.Diagnostic is not null)
            {
                spc.ReportDiagnostic(t.Diagnostic);
                return;
            }

            if (t.Entity is null)
                return;

            try
            {
                GenerateSourceForEntity(spc, t.Entity);
            }
            catch (Exception ex)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.GenerationError,
                    Location.None,
                    t.Entity.TypeName,
                    ex.Message));
            }
        });
    }

    private static void GenerateSourceForEntity(SourceProductionContext context, EntityInfo entity)
    {
        if (string.IsNullOrWhiteSpace(entity.TypeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidEntityType,
                Location.None,
                "Unknown",
                "Entity type name is null or empty"));
            return;
        }

        var source = GenerateQueryCode(entity);

        if (string.IsNullOrWhiteSpace(source))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GenerationError,
                Location.None,
                entity.TypeName,
                "Generated source code is null or empty"));
            return;
        }

        var fileName = $"{entity.TypeName}.g.cs";
        context.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateQueryCode(EntityInfo entityInfo)
    {
        var sb = new StringBuilder(GeneratorConstants.LargeStringBuilderCapacity);

        // File header
        sb.AppendLine(GeneratorConstants.GeneratedCodeComment);
        sb.AppendLine(GeneratorConstants.NullableEnable);
        sb.AppendLine();

        // Usings
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using CursorPaginator.Core;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Runtime.InteropServices;");

        sb.AppendLine($"using {entityInfo.FilterNamespace};");

        if (entityInfo.FilterNamespace != entityInfo.ResponseNamespace)
            sb.AppendLine($"using {entityInfo.ResponseNamespace};");

        sb.AppendLine();

        // Namespace
        if (!string.IsNullOrEmpty(entityInfo.Namespace))
        {
            sb.AppendLine($"namespace {entityInfo.Namespace};");
            sb.AppendLine();
        }

        var providerConfig = SqlProviderConfig.ForProvider(entityInfo.DatabaseProvider);

        // FilterRecordGenerator.GenerateFilterRecord(sb, entityInfo, providerConfig);
        // sb.AppendLine();

        // QueryRecordGenerator.GenerateQueryRecord(sb, entityInfo, providerConfig);
        // sb.AppendLine();

        // ResponseRecordGenerator.GenerateResponseRecord(sb, entityInfo, providerConfig);
        // sb.AppendLine();

        HandlerClassGenerator.GenerateHandlerClass(sb, entityInfo, providerConfig);

        return sb.ToString();
    }
}