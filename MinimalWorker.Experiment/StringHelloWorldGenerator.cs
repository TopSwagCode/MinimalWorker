
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MinimalWorker.Experiment
{
    
    [Generator]
    public class StringHelloWorldGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1) Find all InvocationExpressions like someString.HelloWorld(...)
            var helloWorldCalls = context.SyntaxProvider
                .CreateSyntaxProvider(
                    // predicate: filter to InvocationExpressions whose member name is "HelloWorld"
                    (node, ct) =>
                        node is InvocationExpressionSyntax invoke &&
                        invoke.Expression is MemberAccessExpressionSyntax member &&
                        member.Name.Identifier.Text == "HelloWorld",
                    // transform: cast to InvocationExpressionSyntax
                    (ctx, ct) => (InvocationExpressionSyntax)ctx.Node
                )
                .Collect(); // gather them all

            // 2) Once we know there's at least one HelloWorld call, emit our extension
            context.RegisterSourceOutput(helloWorldCalls, (spc, invocations) =>
            {
                if (invocations.Length == 0)
                    return;

                var source = new StringBuilder(@"
namespace HelloWorldGenerated
{
    public static class StringExtensions
    {
        /// <summary>
        /// Prints ""Hello, {yourString}!"" to the console.
        /// </summary>
        public static void HelloWorld(this string str)
        {
            System.Console.WriteLine($""Hello, {str}!"");
        }
    }
}
");

                spc.AddSource("StringExtensions.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
            });
        }
    }

}