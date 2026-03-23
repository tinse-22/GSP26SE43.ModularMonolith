using ClassifiedAds.Modules.TestReporting.Services;
using DinkToPdf;
using DinkToPdf.Contracts;
using RazorLight;
using System.Linq;

namespace ClassifiedAds.UnitTests.TestReporting;

public class PdfReportRendererTests
{
    [Fact]
    public async Task RenderAsync_ShouldDelegateToConverter()
    {
        // Arrange
        var document = ReportTestData.CreateDocument();
        var engineMock = new Mock<IRazorLightEngine>();
        engineMock.Setup(x => x.CompileRenderStringAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ClassifiedAds.Modules.TestReporting.Models.TestRunReportDocumentModel>(),
                It.IsAny<System.Dynamic.ExpandoObject>()))
            .ReturnsAsync("<html><body>pdf</body></html>");

        var converterMock = new Mock<IConverter>();
        IDocument capturedDocument = null;
        converterMock.Setup(x => x.Convert(It.IsAny<IDocument>()))
            .Callback<IDocument>(doc => capturedDocument = doc)
            .Returns(new byte[] { 1, 2, 3, 4 });

        var htmlRenderer = new HtmlReportRenderer(engineMock.Object);
        var pdfRenderer = new PdfReportRenderer(htmlRenderer, converterMock.Object);

        // Act
        var result = await pdfRenderer.RenderAsync(document);

        // Assert
        result.FileName.Should().EndWith(".pdf");
        result.ContentType.Should().Be("application/pdf");
        result.Content.Should().Equal(new byte[] { 1, 2, 3, 4 });
        converterMock.Verify(x => x.Convert(It.IsAny<IDocument>()), Times.Once);
        capturedDocument.Should().BeOfType<HtmlToPdfDocument>();
        var pdfDocument = (HtmlToPdfDocument)capturedDocument;
        pdfDocument.GlobalSettings.DocumentTitle.Should().Be(document.FileBaseName);
        pdfDocument.Objects.Should().ContainSingle();
        pdfDocument.Objects.Single().HtmlContent.Should().Be("<html><body>pdf</body></html>");
    }
}
