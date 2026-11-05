# Web Angular

Ver preparación, alcance y comandos en el [README del proyecto](../README.md).

La institución se carga desde `/api/v1/bootstrap`, cuya fuente es el único `config/institution.json` del host. No agregar nombres, teléfonos ni rutas de marca de un colegio a los componentes.

`npm run build` compila y copia la web al host .NET. `npm start` utiliza el proxy al host en 5080. En esta máquina usar el wrapper `../scripts/Invoke-Web.ps1` para ejecutar con Node compatible.

`npm test` ejecuta Playwright en escritorio y móvil contra la web compilada; requiere haberla construido e instalado Chromium. La muestra visual está separada del espacio autenticado y se habilita únicamente en Development.
