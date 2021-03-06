﻿namespace IDisposableAnalyzers.NetCoreTests.IDISP001DisposeCreatedTests
{
    using Gu.Roslyn.Asserts;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using NUnit.Framework;

    [TestFixture(typeof(LocalDeclarationAnalyzer))]
    [TestFixture(typeof(ArgumentAnalyzer))]
    [TestFixture(typeof(AssignmentAnalyzer))]
    public static class Valid<T>
        where T : DiagnosticAnalyzer, new()
    {
        private static readonly DiagnosticAnalyzer Analyzer = new T();

        [Test]
        public static void LocalDisposeAsync()
        {
            var code = @"
namespace N
{
    using System.IO;
    using System.Threading.Tasks;

    public class C
    {
        public async ValueTask M()
        {
            var x = File.OpenRead(string.Empty);
            await x.DisposeAsync();
        }
    }
}";

            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void LocalDisposeAsyncInFinally()
        {
            var code = @"
namespace N
{
    using System.IO;
    using System.Threading.Tasks;

    public class C
    {
        public async ValueTask M()
        {
            var x = File.OpenRead(string.Empty);
            try
            {

            }
            finally
            {
                await x.DisposeAsync();
            }
        }
    }
}";

            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void IServiceProviderGetRequiredService()
        {
            var code = @"
namespace N
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class C
    {
        private readonly IServiceProvider serviceProvider;

        public C(IServiceProvider serviceProvider)
        {
            var disposable1 = serviceProvider.GetRequiredService<Disposable>();
            _ = serviceProvider.GetRequiredService<Disposable>();
            var loggerFactory1 = serviceProvider.GetRequiredService<ILoggerFactory>();
            _ = serviceProvider.GetRequiredService<ILoggerFactory>();

            this.serviceProvider = serviceProvider;
            var disposable2 = this.serviceProvider.GetRequiredService<Disposable>();
            _ = this.serviceProvider.GetRequiredService<Disposable>();
            var loggerFactory2 = this.serviceProvider.GetRequiredService<ILoggerFactory>();
            _ = this.serviceProvider.GetRequiredService<ILoggerFactory>();
        }

        public sealed class Disposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}";

            var nullableContextOptions = CodeFactory.DefaultCompilationOptions(Analyzer, null).WithNullableContextOptions(NullableContextOptions.Enable);
            RoslynAssert.Valid(Analyzer, code, compilationOptions: nullableContextOptions);
        }
    }
}
