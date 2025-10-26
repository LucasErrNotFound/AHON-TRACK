using System;
using System.IO;
using AHON_TRACK.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AHON_TRACK.Services;

public class SupplierDocument : IDocument
{
    public SupplierDocumentModel Model { get; }

    public SupplierDocument(SupplierDocumentModel model)
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
                    .Text("SUPPLIER LIST REPORT")
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
                text.Span("Total Suppliers in List: ").FontSize(14).SemiBold();
                text.Span($"{Model.TotalSuppliers:N0}").FontSize(14).SemiBold().FontColor(Colors.Green.Darken2);
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
                columns.RelativeColumn(55);
                columns.ConstantColumn(75);
                columns.RelativeColumn(70);
                columns.RelativeColumn(45);
                columns.RelativeColumn(50);
                columns.RelativeColumn(40);
                columns.RelativeColumn(40);
                columns.RelativeColumn(40);
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyle).Text("ID").FontSize(10);
                header.Cell().Element(CellStyle).Text("Supplier Name").FontSize(10);
                header.Cell().Element(CellStyle).Text("Contact Person").FontSize(10);
                header.Cell().Element(CellStyle).AlignLeft().Text("Email").FontSize(10);
                header.Cell().Element(CellStyle).AlignLeft().Text("Phone Number").FontSize(10);
                header.Cell().Element(CellStyle).Text("Products").FontSize(10);
                header.Cell().Element(CellStyle).Text("Delivery Schedule").FontSize(10);
                header.Cell().Element(CellStyle).Text("Contract Terms").FontSize(10);
                header.Cell().Element(CellStyle).AlignLeft().Text("Status").FontSize(10);

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
                table.Cell().Element(CellStyle).Text(item.ID.ToString()).FontSize(9).SemiBold();
                table.Cell().Element(CellStyle).Text(item.Name).FontSize(9);
                table.Cell().Element(CellStyle).Text(item.ContactPerson).FontSize(9);
                table.Cell().Element(CellStyle).Text(item.Email).FontSize(9);
                table.Cell().Element(CellStyle).Text(item.PhoneNumber).FontSize(9);
                table.Cell().Element(CellStyle).Text(item.Products).FontSize(9);
                table.Cell().Element(CellStyle).Text(item.DeliverySchedule).FontSize(9);
                table.Cell().Element(CellStyle).Text(item.ContractTerms).FontSize(9);
                table.Cell().Element(CellStyle).AlignLeft().Text(item.Status).FontSize(9);
                
                /* This might be the delivery schedule thingy
                table.Cell().Element(CellStyle)
                    .Text(item.PurchasedDate.HasValue ? item.DeliveryDate.Value.ToString("MM/dd/yy") : string.Empty)
                    .FontSize(9);
                */
                

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