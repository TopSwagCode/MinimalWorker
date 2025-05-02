using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MinimalWorker.Experiment
{
    [Generator]
    public class HelloWorldGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Registers a callback that runs once, before any syntax is even analyzed.
            context.RegisterPostInitializationOutput(ctx =>
            {
                const string source = @"
namespace HelloWorldGenerated
{
    public static class HelloWorld
    {
        public static void SayHello() =>
            System.Console.WriteLine(""Hello from generated code!"");
    }
}";
                ctx.AddSource(
                    hintName: "helloWorldGenerated",
                    sourceText: SourceText.From(source, Encoding.UTF8));
            });
        }
    }
}