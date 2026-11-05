namespace SchoolPlatform.Host.Preview;

internal static class PreviewData
{
    public static object Create() => new
    {
        isSample = true,
        academicYear = "2026",
        attendance = new[] { new { month = "Feb", value = 91 }, new { month = "Mar", value = 94 },
            new { month = "Abr", value = 89 }, new { month = "May", value = 96 },
            new { month = "Jun", value = 93 }, new { month = "Jul", value = 95 } },
        students = new[]
        {
            new { code = "DEMO-001", name = "Ana Sofía Morales", grade = "1.º básico", status = "Activo", initials = "AM" },
            new { code = "DEMO-002", name = "Diego Andrés López", grade = "2.º básico", status = "Activo", initials = "DL" },
            new { code = "DEMO-003", name = "Valentina Pérez", grade = "1.º básico", status = "Activo", initials = "VP" },
            new { code = "DEMO-004", name = "Mateo García", grade = "3.º básico", status = "Pendiente", initials = "MG" },
            new { code = "DEMO-005", name = "Camila Hernández", grade = "2.º básico", status = "Activo", initials = "CH" },
            new { code = "DEMO-006", name = "Sebastián Reyes", grade = "3.º básico", status = "Activo", initials = "SR" }
        }
    };
}
