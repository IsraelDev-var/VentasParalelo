using VentasParalelo.Core.Models;
using Xunit;

namespace VentasParalelo.Core.Tests;

public class AggregationResultMergeTests
{
    [Fact]
    public void MergeFrom_SumaAcumuladoresYFilasProcesadas()
    {
        var destino = new AggregationResult { FilasProcesadas = 10 };
        destino.MontoPorSucursal["Santiago"] = 100m;
        destino.UnidadesPorProducto["Panel Solar 450W"] = 5;

        var origen = new AggregationResult { FilasProcesadas = 3 };
        origen.MontoPorSucursal["Santiago"] = 50m;
        origen.MontoPorSucursal["La Vega"] = 20m;
        origen.UnidadesPorProducto["Panel Solar 450W"] = 2;

        destino.MergeFrom(origen);

        Assert.Equal(13, destino.FilasProcesadas);
        Assert.Equal(150m, destino.MontoPorSucursal["Santiago"]);
        Assert.Equal(20m, destino.MontoPorSucursal["La Vega"]);
        Assert.Equal(7, destino.UnidadesPorProducto["Panel Solar 450W"]);
    }
}
