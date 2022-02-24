using System;
using System.Collections.Generic;
using HotChocolate;
using HotChocolate.Language;
using HotChocolate.Validation;
using Microsoft.Extensions.DependencyInjection;
using static StrawberryShake.CodeGeneration.Utilities.NameUtils;

namespace StrawberryShake.CodeGeneration.Utilities;

/// <summary>
/// Merges all documents and create one query document per operation.
/// </summary>
internal static class OperationDocumentHelper
{
    /// <summary>
    /// Merges the documents and creates operation documents that
    /// can be used for the actual requests.
    /// </summary>
    /// <param name="schema">
    /// The schema to validate queries against.
    /// </param>
    /// <param name="documents">
    /// The GraphQL documents.
    /// </param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static OperationDocuments CreateOperationDocuments(
        ISchema schema,
        IEnumerable<DocumentNode> documents)
    {
        if (documents is null)
        {
            throw new ArgumentNullException(nameof(documents));
        }

        DocumentNode mergedDocument = MergeDocuments(documents);
        mergedDocument = RemovedUnusedFragmentRewriter.Rewrite(mergedDocument);

        IDocumentValidator validator =
            new ServiceCollection()
                .AddValidation()
                .Services
                .BuildServiceProvider()
                .GetRequiredService<IDocumentValidatorFactory>()
                .CreateValidator();

        DocumentValidatorResult result = validator.Validate(schema, mergedDocument);

        if (result.HasErrors)
        {
            throw new GraphQLException(result.Errors);
        }

        Dictionary<string, DocumentNode> operationDocs = ExportOperations(mergedDocument);
        return new OperationDocuments(mergedDocument, operationDocs);
    }

    private static DocumentNode MergeDocuments(IEnumerable<DocumentNode> documents)
    {
        var definitions = new List<IDefinitionNode>();

        foreach (DocumentNode document in documents)
        {
            foreach (IDefinitionNode definition in document.Definitions)
            {
                if (definition is OperationDefinitionNode { Name: { } name } op)
                {
                    name = name.WithValue(GetClassName(name.Value));
                    op = op.WithName(name);
                    definitions.Add(op);
                }
                else
                {
                    definitions.Add(definition);
                }
            }
        }

        ValidateDocument(definitions);

        return new DocumentNode(definitions);
    }

    private static void ValidateDocument(IEnumerable<IDefinitionNode> definitions)
    {
        var operationNames = new HashSet<string>();
        var fragmentNames = new HashSet<string>();

        foreach (var definition in definitions)
        {
            if (definition is OperationDefinitionNode op)
            {
                if (op.Name is null)
                {
                    throw new CodeGeneratorException(
                        ErrorBuilder.New()
                            .SetMessage("All operations must be named.")
                            .AddLocation(op)
                            .Build());
                }

                if (!operationNames.Add(op.Name.Value))
                {
                    throw new CodeGeneratorException(
                        ErrorBuilder.New()
                            .SetMessage(
                                "The operation name `{0}` is not unique.",
                                op.Name.Value)
                            .AddLocation(op)
                            .Build());
                }
            }

            if (definition is FragmentDefinitionNode fd)
            {
                if (!fragmentNames.Add(fd.Name.Value))
                {
                    throw new CodeGeneratorException(
                        ErrorBuilder.New()
                            .SetMessage(
                                "The fragment name `{0}` is not unique.",
                                fd.Name.Value)
                            .AddLocation(fd)
                            .Build());
                }
            }
        }
    }

    private static Dictionary<string, DocumentNode> ExportOperations(DocumentNode document)
    {
        var visitor = new ExtractOperationVisitor();
        var context = new ExtractOperationContext(document);
        var operationDocs = new Dictionary<string, DocumentNode>();

        do
        {
            visitor.Visit(context.Operation, context);

            var definitions = new List<IDefinitionNode> { context.Operation };
            definitions.AddRange(context.ExportedFragments);
            var operationDoc = new DocumentNode(definitions);
            operationDocs.Add(context.Operation.Name!.Value, operationDoc);
        } while (context.Next());

        return operationDocs;
    }
}
