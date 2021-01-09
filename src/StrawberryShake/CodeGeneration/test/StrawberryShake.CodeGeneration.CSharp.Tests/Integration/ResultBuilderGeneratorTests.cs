using System.Text;
using System.Threading.Tasks;
using Snapshooter.Xunit;
using Xunit;

namespace StrawberryShake.CodeGeneration.CSharp.Tests.Integration
{
    public class ResultBuilderGeneratorTests
    {
        readonly StringBuilder _stringBuilder;
        readonly CodeWriter _codeWriter;
        readonly JsonResultBuilderGenerator _generator;

        public ResultBuilderGeneratorTests()
        {
            _stringBuilder = new StringBuilder();
            _codeWriter = new CodeWriter(_stringBuilder);
            _generator = new JsonResultBuilderGenerator();
        }

        [Fact]
        public async Task GenerateResultBuilder()
        {
            await _generator.WriteAsync(
                _codeWriter,
                IntegrationDescriptors.GetHeroResultBuilderDescriptor
            );
            _stringBuilder.ToString().MatchSnapshot();
        }
    }
}
