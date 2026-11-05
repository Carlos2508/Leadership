# ADR-001: base técnica y distribución

Estado: adoptado para comenzar. Fecha: 4 de noviembre de 2026.

El propietario pide reconstrucción y reutilización del proyecto para otro colegio cambiando configuración. Aclara expresamente que cada colegio recibirá **su propio proyecto**, no una cuenta dentro de un SaaS.

Se adopta un monolito modular ASP.NET Core con Angular y SQL Server. Cada instalación pertenece a una institución y tiene su base independiente; no hay resolución de clientes, tenants, membresías entre colegios ni base compartida del producto. La marca se lee del único `config/institution.json`. Los futuros cambios de sede/ciclo pertenecen a la misma institución.

Se elige Angular por las futuras grillas de notas, tareas y formularios interactivos, y porque el kit ya ofrece tokens/prototipos para esa dirección. Razor se reserva para autenticación. Esto exige mantener frontend/backend y contratos; no se construye en paralelo una segunda web completa MVC.

SQL Server es el motor transaccional elegido y se usa ya en las pruebas locales. En esta máquina hay una instancia accesible por autenticación integrada; se creó únicamente `SchoolPlatform_Development`. Edición/licencia, hosting y operaciones de producción quedan pendientes de ADR específico cuando se prepare ese ambiente.

Redis, Flutter, OpenIddict y workers se incorporarán por dependencia funcional, no por anticipación. No se agregaron proyectos vacíos para esos módulos.
