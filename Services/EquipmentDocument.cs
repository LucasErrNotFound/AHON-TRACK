using System;
using System.IO;
using AHON_TRACK.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AHON_TRACK.Services;

public class EquipmentDocument : IDocument
{
    public EquipmentDocumentModel Model { get; }

    public EquipmentDocument(EquipmentDocumentModel model)
    {
        Model = model;
    }
    
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;
    
    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Margin(15);
                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
    }
    
    void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\.."));
                string path = Path.Combine(projectRoot, "Assets", "LoginView", "AHON-Track-Secondary-Logo.png");
                
                column.Item().Width(150).Image(path).UseOriginalImage();
                
                column.Item()
                    .Text(Model.GymName)
                    .FontSize(20)
                    .SemiBold()
                    .FontColor(Colors.Black);

                column.Item().Text(text =>
                {
                    text.Span("Address: ").SemiBold().FontColor(Colors.Blue.Medium);
                    text.Span(Model.GymAddress);
                });

                column.Item().Text(text =>
                {
                    text.Span("Phone: ").SemiBold().FontColor(Colors.Blue.Medium);
                    text.Span(Model.GymPhone);
                });

                column.Item().Text(text =>
                {
                    text.Span("Email: ").SemiBold().FontColor(Colors.Blue.Medium);
                    text.Span(Model.GymEmail);
                });
            });

            row.RelativeItem().Column(column =>
            {
                column.Item().AlignRight()
                    .Text("EQUIPMENT LIST REPORT")
                    .FontColor(Colors.Red.Medium)
                    .FontSize(18)
                    .SemiBold();

                column.Item().AlignRight().Text(text =>
                {
                    text.Span("Generated on: ").SemiBold().FontColor(Colors.Blue.Medium);
                    text.Span($"{Model.GeneratedDate:MMMM dd, yyyy}");
                });
            });
        });
    }
    
    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(40).Column(column =>
        {
            column.Spacing(5);
            column.Item().Element(ComposeTable);
            column.Item().PaddingTop(10).AlignRight().Text(text =>
            {
                text.Span("Total Equipment in List: ").FontSize(14).SemiBold();
                text.Span($"{Model.TotalEquipments:N0}").FontSize(14).SemiBold().FontColor(Colors.Green.Darken2);
            });
        });
    }
    
    void ComposeTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(30);
                columns.RelativeColumn(60);
                columns.ConstantColumn(50);
                columns.RelativeColumn(40);
                columns.ConstantColumn(25);
                columns.RelativeColumn(50);
                columns.RelativeColumn(40);
                columns.RelativeColumn(40);
                columns.RelativeColumn(40);
                columns.RelativeColumn(40);
                columns.RelativeColumn(40);
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyle).Text("ID").FontSize(9);
                header.Cell().Element(CellStyle).Text("Brand Name").FontSize(9);
                header.Cell().Element(CellStyle).Text("Category").FontSize(9);
                header.Cell().Element(CellStyle).AlignLeft().Text("Supplier").FontSize(9);
                header.Cell().Element(CellStyle).Text("Stock").FontSize(9);
                header.Cell().Element(CellStyle).Text("Purchased Price").FontSize(9);
                header.Cell().Element(CellStyle).Text("Purchased Date").FontSize(9);
                header.Cell().Element(CellStyle).Text("Warranty").FontSize(9);
                header.Cell().Element(CellStyle).AlignLeft().Text("Condition").FontSize(9);
                header.Cell().Element(CellStyle).AlignLeft().Text("Last Maintenance").FontSize(9);
                header.Cell().Element(CellStyle).AlignLeft().Text("Next Maintenance").FontSize(9);

                static IContainer CellStyle(IContainer container)
                {
                    return container
                        .DefaultTextStyle(x => x.SemiBold())
                        .PaddingVertical(5)
                        .BorderBottom(1)
                        .BorderColor(Colors.Black);
                }
            });

            foreach (var item in Model.Items)
            {
                table.Cell().Element(CellStyle).Text(item.ID.ToString()).FontSize(8).SemiBold();
                table.Cell().Element(CellStyle).Text(item.BrandName).FontSize(8);
                table.Cell().Element(CellStyle).Text(item.Category).FontSize(8);
                table.Cell().Element(CellStyle).Text(item.Supplier).FontSize(8);
                table.Cell().Element(CellStyle).Text(item.CurrentStock).FontSize(8);
                table.Cell().Element(CellStyle).AlignLeft().Text($"₱{item.PurchasedPrice:N2}").FontSize(8);
                
                table.Cell().Element(CellStyle)
                    .Text(item.PurchasedDate.HasValue ? item.PurchasedDate.Value.ToString("MM/dd/yy") : string.Empty)
                    .FontSize(8);
                
                table.Cell().Element(CellStyle)
                    .Text(item.Warranty.HasValue ? item.Warranty.Value.ToString("MM/dd/yy") : string.Empty)
                    .FontSize(8);
                
                table.Cell().Element(CellStyle).AlignLeft().Text(item.Condition).FontSize(8);
                
                table.Cell().Element(CellStyle)
                    .Text(item.LastMaintenance.HasValue ? item.LastMaintenance.Value.ToString("MM/dd/yy") : string.Empty)
                    .FontSize(8);
                
                table.Cell().Element(CellStyle)
                    .Text(item.NextMaintenance.HasValue ? item.NextMaintenance.Value.ToString("MM/dd/yy") : string.Empty)
                    .FontSize(8);

                static IContainer CellStyle(IContainer container)
                {
                    return container
                        .BorderBottom(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .PaddingVertical(5);
                }
            }
        });
    }
}