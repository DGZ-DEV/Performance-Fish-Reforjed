using System.Collections.Generic;
using PerformanceFishReforjed.Prepatch;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Cache del resultado de <c>WorkGiver_DoBill.get_PotentialWorkThingRequest</c>. El valor es
    /// constante por instancia de WorkGiver_DoBill (depende solo de su <see cref="WorkGiver.def"/>, y
    /// los defs fijos de un WorkGiverDef no cambian en runtime), así que se resuelve una sola vez y se
    /// reutiliza en cada escaneo de trabajo.
    /// </summary>
    public struct WorkRequestCache
    {
        /// <summary>El valor ya se ha resuelto una vez.</summary>
        public bool Valid;

        /// <summary>ThingRequest cacheado.</summary>
        public ThingRequest Request;
    }

    /// <summary>
    /// Cuerpos que sustituyen a los métodos de <see cref="WorkGiver_DoBill"/> en 1.6.
    ///
    /// La semántica está copiada del IL real del juego (informe:
    /// <c>Tools/VANILLA_IL_WORKGIVER.md</c>), no de la referencia del mod original. Cada método
    /// reproduce exactamente el flujo vanilla:
    ///
    /// <list type="bullet">
    /// <item><c>GetBillGiverRootCell</c>: devuelve la InteractionCell si el bill giver es un edificio
    /// con celda de interacción; si el edificio no la tiene, registra el mismo error que el vanilla y
    /// devuelve <c>forPawn.Position</c>; si no es edificio, devuelve <c>thing.Position</c>.</item>
    /// <item><c>GetMedicalCareCategory</c>: para un Pawn con playerSettings devuelve
    /// <c>playerSettings.medCare</c>; en otro caso <c>MedicalCareCategory.Best</c>.</item>
    /// <item><c>ThingIsUsableBillGiver</c>: combina defs fijos, banderas de tipo de pawn y banderas de
    /// cadáveres exactamente como el vanilla (order y nulls incluidos).</item>
    /// <item><c>ShouldSkip</c>: recorre ThingsInGroup(PotentialBillGiver) y devuelve true si ningún
    /// bill giver tiene un bill que hacer ahora.</item>
    /// <item><c>IsUsableIngredient</c>: <c>IsFixedOrAllowedIngredient</c> más el primer filtro de
    /// ingrediente de la receta que permite la cosa.</item>
    /// <item><c>TryFindBestBillIngredientsInSet</c>: despacha a AllowMix o NoMix según
    /// <c>recipe.allowMixingIngredients</c>.</item>
    /// </list>
    ///
    /// El resto de métodos (los pesados de selección de ingredientes con closures:
    /// <c>TryFindBestBillIngredientsInSet_AllowMix</c>, <c>TryFindBestIngredientsInSet_NoMixHelper</c>,
    /// <c>TryFindBestIngredientsHelper</c> y compañía) NO se sustituyen: dependen de las clases
    /// generadas por el compilador para los closures, y copiar sus cuerpos sería justo el error que
    /// desaconseja el análisis de este proyecto. Se dejan intactos para garantizar que el
    /// comportamiento de la selección de ingredientes sea exactamente el vanilla.
    /// </summary>
    public static class WorkGiverDoBillOptimization
    {
        // ─── get_PotentialWorkThingRequest (instancia) ─────────────────────────────

        /// <summary>Reemplazo de <c>get_PotentialWorkThingRequest</c>, con resultado cacheado.</summary>
        public static ThingRequest GetPotentialWorkThingRequest(WorkGiver_DoBill wg)
        {
            ref WorkRequestCache cache = ref wg.ReforjedWorkRequestCache();
            if (cache.Valid)
                return cache.Request;

            ThingRequest request = wg.def.fixedBillGiverDefs is { Count: 1 }
                ? ThingRequest.ForDef(wg.def.fixedBillGiverDefs[0])
                : ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver);

            cache = new WorkRequestCache { Valid = true, Request = request };
            return request;
        }

        // ─── MaxPathDanger (instancia) ─────────────────────────────────────────────

        /// <summary>Reemplazo de <c>MaxPathDanger</c>: el vanilla devuelve siempre <c>Danger.Some</c>.</summary>
        public static Danger MaxPathDanger(WorkGiver_DoBill wg, Pawn pawn)
            => Danger.Some;

        // ─── GetBillGiverRootCell (estática) ────────────────────────────────────────

        /// <summary>Reemplazo de <c>GetBillGiverRootCell(Thing, Pawn)</c>.</summary>
        public static IntVec3 GetBillGiverRootCell(Thing thing, Pawn forPawn)
        {
            if (thing is Building building)
            {
                if (building.def.hasInteractionCell)
                    return building.InteractionCell;

                Log.Error("Tried to find bill ingredients for " + thing.ToString()
                          + " which has no interaction cell.");
                return forPawn.Position;
            }

            return thing.Position;
        }

        // ─── GetMedicalCareCategory (estática) ──────────────────────────────────────

        /// <summary>Reemplazo de <c>GetMedicalCareCategory(Thing)</c>.</summary>
        public static MedicalCareCategory GetMedicalCareCategory(Thing thing)
        {
            if (thing is Pawn pawn && pawn.playerSettings != null)
                return pawn.playerSettings.medCare;

            return MedicalCareCategory.Best;
        }

        // ─── ThingIsUsableBillGiver (instancia) ─────────────────────────────────────

        /// <summary>Reemplazo de <c>ThingIsUsableBillGiver(Thing)</c>.</summary>
        public static bool ThingIsUsableBillGiver(WorkGiver_DoBill wg, Thing thing)
        {
            Pawn pawn = thing as Pawn;
            Corpse corpse = thing as Corpse;
            Pawn innerPawn = corpse != null ? corpse.InnerPawn : null;

            if (wg.def.fixedBillGiverDefs != null && wg.def.fixedBillGiverDefs.Contains(thing.def))
                return true;

            if (pawn != null)
            {
                if (wg.def.billGiversAllHumanlikes && pawn.RaceProps.Humanlike)
                    return true;
                if (wg.def.billGiversAllMechanoids && pawn.RaceProps.IsMechanoid)
                    return true;
                if (wg.def.billGiversAllAnimals && pawn.IsAnimal)
                    return true;
            }

            if (corpse != null && innerPawn != null)
            {
                if (wg.def.billGiversAllHumanlikesCorpses && innerPawn.RaceProps.Humanlike)
                    return true;
                if (wg.def.billGiversAllMechanoidsCorpses && innerPawn.RaceProps.IsMechanoid)
                    return true;
                if (wg.def.billGiversAllAnimalsCorpses && innerPawn.IsAnimal)
                    return true;
            }

            return false;
        }

        // ─── ShouldSkip (instancia) ─────────────────────────────────────────────────

        /// <summary>Reemplazo de <c>ShouldSkip(Pawn, Boolean)</c>.</summary>
        public static bool ShouldSkip(WorkGiver_DoBill wg, Pawn pawn, bool forced)
        {
            List<Thing> things = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.PotentialBillGiver);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is not IBillGiver billGiver)
                    continue;
                if (ReferenceEquals(billGiver, pawn))
                    continue;
                if (!ThingIsUsableBillGiver(wg, things[i]))
                    continue;
                if (!billGiver.BillStack.AnyShouldDoNow)
                    continue;

                return false;
            }

            return true;
        }

        // ─── IsUsableIngredient (estática) ──────────────────────────────────────────

        /// <summary>Reemplazo de <c>IsUsableIngredient(Thing, Bill)</c>.</summary>
        public static bool IsUsableIngredient(Thing thing, Bill bill)
        {
            if (!bill.IsFixedOrAllowedIngredient(thing))
                return false;

            foreach (IngredientCount ingredient in bill.recipe.ingredients)
            {
                if (ingredient.filter.Allows(thing))
                    return true;
            }

            return false;
        }

        // ─── TryFindBestBillIngredientsInSet (estática) ─────────────────────────────

        /// <summary>
        /// Reemplazo de <c>TryFindBestBillIngredientsInSet</c>: despacha a la variante AllowMix o
        /// NoMix según <c>recipe.allowMixingIngredients</c>.
        /// </summary>
        public static bool TryFindBestBillIngredientsInSet(List<Thing> availableThings, Bill bill,
            List<ThingCount> chosen, IntVec3 rootCell, bool alreadySorted,
            List<IngredientCount> missingIngredients)
        {
            if (bill.recipe.allowMixingIngredients)
                return WorkGiver_DoBill.TryFindBestBillIngredientsInSet_AllowMix(
                    availableThings, bill, chosen, rootCell, missingIngredients);

            return WorkGiver_DoBill.TryFindBestBillIngredientsInSet_NoMix(
                availableThings, bill, chosen, rootCell, alreadySorted, missingIngredients);
        }
    }
}