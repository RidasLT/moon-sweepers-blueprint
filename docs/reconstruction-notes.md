# SVARBU: Šis kodas atkurtas iš atminties

Šie failai buvo atkurti iš ankstesnės Claude sesijos konteksto sumaros.
Originalus kodas buvo tik sesijų konteineriuose, kurie jau išvalyti.

## Kas tikslu:
- Schematic formatas (Belt su Via/ViaDirections)
- NativeBuild.Place logika (ConveyorPlan be Via, TryBuildConveyor)
- GhostModel.BeltBar segmentinis preview (crash workaround)
- Session konstantos (DefaultClearHeight=140, MaxHeight=200)
- Plugin versija 1.0.38, schematic folder migration
- Loc vertimų raktai ir LT tekstai
- Testų struktūra

## Kas apytiksliai:
- PlacementSnap — logika atkurta, bet smulkios detalės (pair-branch rescue, 
  horizontal safety net) gali skirtis nuo originalo
- Controller — supaprastintas, originalas turėjo daugiau logikos
- Markers — supaprastintas, originalas naudojo Unity GL API
- Session.Volume — placeholder implementacija, originalas turėjo pilną 
  4-kampių poligono + aukščio tikrinimą
- Tests — atkurti 13 iš 41 originalių testų

## Ko TRŪKSTA:
- Holo.cs (hologramos renderingas)
- Overhead.cs (top-view režimas)
- PlacementDiag.cs (diagnostikos logai)
- Capture.cs (kopijavimo logika — 4 kampų atrinkimas)
- Library.cs (schematų biblioteka su thumbnails)
- BuriedFilter.cs (žemės paviršiaus skenavimas)
- .csproj failas (kompiliavimo nustatymai)

Sukompiliuotas DLL (v1.0.38) yra: Moon Sweepers Blueprint.dll
