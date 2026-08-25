# Contribuir a CA-O

## Reglas de oro

1. **Evidencia o no recomendación**: un cambio sin evidencia moderna verificable entra como Experimental (o no entra). Nunca como Recommended.
2. **Sin claims numéricos**: prohibido prometer FPS/latencia en descripciones; el contrato de tests lo rechaza.
3. **Transaccional obligatorio**: toda optimización nueva debe soportar Detect/Capture/Apply/Revert y clasificación completa (Evidence/Risk/SecurityImpact/Compatibility).
4. **Seguridad no es moneda de cambio**: si reduces seguridad, eres SecurityTradeoff + ExpertOnly, y con anti-cheat presente quedas bloqueado por defecto.
5. **Privilegio mínimo**: la UI no eleva; las operaciones nuevas del servicio requieren tipo fuerte en `OperationParameters` + validador + test adversarial.

## Flujo

1. Fork/rama desde `main`.
2. Cambios con tests que fallen sin tu parche.
3. `powershell -File scripts\verify.ps1` en verde (build Release + todos los tests + contratos).
4. PR describiendo evidencia del cambio y su impacto de seguridad/compatibilidad.

## Añadir una optimización al catálogo

1. Crear clase en `src\CA-O.Core\Optimizations\<Categoría>\` implementando `IOptimization` (o extender `RegistryOptimizationBase`).
2. Definición completa: Id kebab-case único, NameEs/En, DescriptionEs/En, TooltipEs, Category, ExpectedImpact, Evidence, Risk, Compatibility, SecurityImpact, Flags.
3. El contrato (`OptimizationCatalogContractTests`) valida automáticamente: unicidad, metadatos completos, reversibilidad documentada, marcas de seguridad y ausencia de claims.
4. Si requiere comandos externos, añadir el patrón exacto a `ElevatedCommandCatalog` + casos de prueba.
5. Documentar en `docs/OPTIMIZATION-CATALOG.md`.

## Estilo

- Nullable habilitado; analizadores .NET en nivel latest-recommended.
- Sin comentarios innecesarios; nombres que se expliquen solos.
- Mensajes de usuario SIEMPRE vía `Localizer` (es-ES + en-US).
