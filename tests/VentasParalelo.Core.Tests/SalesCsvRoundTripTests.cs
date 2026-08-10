using VentasParalelo.Core.DataGeneration;
using VentasParalelo.Core.IO;
using Xunit;

namespace VentasParalelo.Core.Tests;

public class SalesCsvRoundTripTests
{
    [Fact]
    public void GenerateCsv_ThenReadAll_ProducesExpectedRowCountAndValidFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ventas_test_{Guid.NewGuid():N}.csv");
        try
        {
            const int filas = 2_000;
            SalesDataGenerator.GenerateCsv(path, filas);

            var registros = SalesCsvReader.ReadAll(path);

            Assert.Equal(filas, registros.Length);
            Assert.All(registros, r =>
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Sucursal));
                Assert.False(string.IsNullOrWhiteSpace(r.Producto));
                Assert.InRange(r.Cantidad, 1, 20);
                Assert.True(r.Monto > 0);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseLine_ParsesAllFieldsCorrectly()
    {
        var record = SalesCsvReader.ParseLine("2025-03-15,Santiago,Inversor 5kW,3,15000.50");

        Assert.Equal(new DateOnly(2025, 3, 15), record.Fecha);
        Assert.Equal("Santiago", record.Sucursal);
        Assert.Equal("Inversor 5kW", record.Producto);
        Assert.Equal(3, record.Cantidad);
        Assert.Equal(15000.50m, record.Monto);
    }
}
