namespace VentasParalelo.Core.Models;

public readonly record struct SaleRecord(
    DateOnly Fecha,
    string Sucursal,
    string Producto,
    int Cantidad,
    decimal Monto);
