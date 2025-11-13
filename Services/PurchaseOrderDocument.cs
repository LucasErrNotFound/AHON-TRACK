using System;
using System.IO;
using AHON_TRACK.Components.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AHON_TRACK.Services;

public class PurchaseOrderDocument : IDocument
{
    public PurchaseOrderDocumentModel Model { get; }

    public PurchaseOrderDocument(PurchaseOrderDocumentModel model)
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
                page.Margin(40);
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
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(logoColumn =>
                {
                    string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\.."));
                    string path = Path.Combine(projectRoot, "Assets", "LoginView", "AHON-Track-Secondary-Logo.png");
                    
                    logoColumn.Item().Width(150).Image(path).UseOriginalImage();
                    
                    logoColumn.Item()
                        .Text(Model.GymName)
                        .FontSize(20)
                        .SemiBold()
                        .FontColor(Colors.Black);
                });

                row.RelativeItem().Column(titleColumn =>
                {
                    titleColumn.Item().AlignRight()
                        .Text("PURCHASE ORDER")
                        .FontColor(Colors.Red.Medium)
                        .FontSize(18)
                        .SemiBold();

                    titleColumn.Item().AlignRight().Text(text =>
                    {
                        text.Span("PO Number: ").SemiBold().FontColor(Colors.Blue.Medium);
                        text.Span(Model.PONumber);
                    });

                    titleColumn.Item().AlignRight().Text(text =>
                    {
                        text.Span("Generated on: ").SemiBold().FontColor(Colors.Blue.Medium);
                        text.Span($"{Model.GeneratedDate:MMMM dd, yyyy}");
                    });
                    
                    if (Model.OrderDate.HasValue)
                    {
                        titleColumn.Item().AlignRight().Text(text =>
                        {
                            text.Span("Order Date: ").SemiBold().FontColor(Colors.Blue.Medium);
                            text.Span($"{Model.OrderDate.Value:MMMM dd, yyyy}");
                        });
                    }
                    
                    if (Model.ExpectedDeliveryDate.HasValue)
                    {
                        titleColumn.Item().AlignRight().Text(text =>
                        {
                            text.Span("Expected Delivery: ").SemiBold().FontColor(Colors.Blue.Medium);
                            text.Span($"{Model.ExpectedDeliveryDate.Value:MMMM dd, yyyy}");
                        });
                    }
                });
            });
            
            column.Item().PaddingTop(20);
            column.Item().BorderBottom(2).BorderColor(Colors.Blue.Medium).PaddingBottom(10);
            column.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(supplierColumn =>
                {
                    supplierColumn.Item()
                        .Text("SUPPLIER INFORMATION")
                        .FontSize(12)
                        .SemiBold()
                        .FontColor(Colors.Blue.Darken2);
                    
                    supplierColumn.Item().PaddingTop(5).Text(text =>
                    {
                        text.Span("Name: ").SemiBold().FontColor(Colors.Red.Darken1);
                        text.Span(Model.SupplierName);
                    });
                    
                    supplierColumn.Item().Text(text =>
                    {
                        text.Span("Contact Person: ").SemiBold().FontColor(Colors.Red.Darken1);
                        text.Span(Model.SupplierContactPerson);
                    });
                    
                    supplierColumn.Item().Text(text =>
                    {
                        text.Span("Address: ").SemiBold().FontColor(Colors.Red.Darken1);
                        text.Span(Model.SupplierAddress);
                    });
                    
                    supplierColumn.Item().Text(text =>
                    {
                        text.Span("Phone: ").SemiBold().FontColor(Colors.Red.Darken1);
                        text.Span(Model.SupplierPhone);
                    });
                    
                    supplierColumn.Item().Text(text =>
                    {
                        text.Span("Email: ").SemiBold().FontColor(Colors.Red.Darken1);
                        text.Span(Model.SupplierEmail);
                    });
                });
                
                // Add some spacing between columns
                row.ConstantItem(30);
                
                // Recipient Details (Right)
                row.RelativeItem().Column(recipientColumn =>
                {
                    recipientColumn.Item()
                        .Text("RECIPIENT INFORMATION")
                        .FontSize(12)
                        .SemiBold()
                        .FontColor(Colors.Blue.Darken2);
                    
                    recipientColumn.Item().PaddingTop(5).Text(text =>
                    {
                        text.Span("Name: ").SemiBold().FontColor(Colors.Red.Darken1);
                        text.Span(Model.Recipient);
                    });
                    
                    recipientColumn.Item().Text(text =>
                    {
                        text.Span("Address: ").SemiBold().FontColor(Colors.Red.Darken1);
                        text.Span(Model.GymAddress);
                    });
                    
                    recipientColumn.Item().Text(text =>
                    {
                        text.Span("Phone: ").SemiBold().FontColor(Colors.Red.Darken1);
                        text.Span(Model.GymPhone);
                    });
                    
                    recipientColumn.Item().Text(text =>
                    {
                        text.Span("Email: ").SemiBold().FontColor(Colors.Red.Darken1);
                        text.Span(Model.GymEmail);
                    });
                });
            });
        });
    }
    
    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(20).Column(column =>
        {
            column.Spacing(5);
            column.Item().Element(ComposeTable);
            
            // Totals section
            column.Item().PaddingTop(20).AlignRight().Column(totalsColumn =>
            {
                totalsColumn.Item().Text(text =>
                {
                    text.Span("Subtotal: ").FontSize(12).SemiBold();
                    text.Span($"₱{Model.Subtotal:N2}").FontSize(12).SemiBold();
                });
                
                totalsColumn.Item().PaddingTop(5).Text(text =>
                {
                    text.Span("VAT (12%): ").FontSize(12).SemiBold();
                    text.Span($"₱{Model.Vat:N2}").FontSize(12).SemiBold();
                });
                
                totalsColumn.Item().PaddingTop(5).BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(5).Text(text =>
                {
                    text.Span("Total: ").FontSize(14).SemiBold();
                    text.Span($"₱{Model.Total:N2}").FontSize(14).SemiBold().FontColor(Colors.Green.Darken2);
                });
            });
            
            // Signature Section - Bottom Right
            column.Item().PaddingTop(40).AlignRight().Width(200).Column(signatureColumn =>
            {
                // Space for signature (you can add an image here if you have a signature file)
                signatureColumn.Item().Height(50).AlignCenter().Text(""); // Space for actual signature
                
                // Signature line
                signatureColumn.Item()
                    .BorderTop(1)
                    .BorderColor(Colors.Black)
                    .PaddingTop(2);
                
                // Printed name
                signatureColumn.Item().AlignCenter().Text(text =>
                {
                    text.Span("Signature over Printed Name")
                        .FontSize(10)
                        .SemiBold();
                });
            });
        });
    }
    
    void ComposeTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3); // Item Name
                columns.RelativeColumn(2); // Unit
                columns.RelativeColumn(1); // Quantity
                columns.RelativeColumn(2); // Unit Price
                columns.RelativeColumn(2); // Total
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyle).Text("Item Name").FontSize(11);
                header.Cell().Element(CellStyle).AlignCenter().Text("Unit").FontSize(11);
                header.Cell().Element(CellStyle).AlignCenter().Text("Quantity").FontSize(11);
                header.Cell().Element(CellStyle).AlignRight().Text("Unit Price").FontSize(11);
                header.Cell().Element(CellStyle).AlignRight().Text("Total").FontSize(11);

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
                var itemTotal = item.Price * item.Quantity;
            
                table.Cell().Element(CellStyle).Text(item.ItemName).FontSize(10);
                table.Cell().Element(CellStyle).AlignCenter().Text(item.Unit).FontSize(10);
                table.Cell().Element(CellStyle).AlignCenter().Text(item.FormattedQuantity).FontSize(10);
                table.Cell().Element(CellStyle).AlignRight().Text($"₱{item.Price:N2}").FontSize(10);
                table.Cell().Element(CellStyle).AlignRight().Text($"₱{itemTotal:N2}").FontSize(10);

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
    
    private string GetStatusColor(string status)
    {
        return status?.ToLowerInvariant() switch
        {
            "paid" or "delivered" => Colors.Green.Darken2,
            "pending" or "unpaid" => Colors.Orange.Darken2,
            "cancelled" => Colors.Red.Darken2,
            "processing" or "shipped/in-transit" => Colors.Blue.Darken2,
            _ => Colors.Grey.Darken2
        };
    }
}