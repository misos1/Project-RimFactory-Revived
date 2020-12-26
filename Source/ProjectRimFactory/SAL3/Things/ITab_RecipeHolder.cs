﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRimFactory.SAL3.Things
{

    interface IRecipeHolderInterface
    {
        //List of Recipes saved on the Recipie Holder
        List<RecipeDef> Saved_Recipes { get; set; }
        //List of Recipes Quered up to be Learnd / Saved to the Recipie Holder
        List<RecipeDef> Quered_Recipes { get; set; }
        //List of Recipes that could be Learnd by the Recipie Holder
        List<RecipeDef> Learnable_Recipes { get; }
        //
        RecipeDef Recipe_Learning { get; set; }

        float Progress_Learning { get; }


    }


    class ITab_RecipeHolder : ITab
    {

        private static readonly Vector2 WinSize = new Vector2(600f, 630f);

        private IRecipeHolderInterface parrentDB => this.SelThing as IRecipeHolderInterface;

        private int itemcount => parrentDB.Learnable_Recipes.Count() + parrentDB.Saved_Recipes.Count();


        enum enum_RecipeStatus
        {
            Saved,
            Quered,
            Learnable,
            InPorogress
        }



        private Dictionary<RecipeDef, enum_RecipeStatus> Recipes { 
        
        get
            {

                Dictionary<RecipeDef, enum_RecipeStatus> lRecipes = new Dictionary<RecipeDef, enum_RecipeStatus>();
                foreach (RecipeDef r in parrentDB.Learnable_Recipes)
                {
                    if (parrentDB.Quered_Recipes.Contains(r))
                    {
                        lRecipes[r] = enum_RecipeStatus.Quered;
                    }else if (parrentDB.Recipe_Learning == r)
                    {
                        lRecipes[r] = enum_RecipeStatus.InPorogress;
                    }
                    else
                    {
                        lRecipes[r] = enum_RecipeStatus.Learnable;
                    }
                }
                foreach (RecipeDef r in parrentDB.Saved_Recipes)
                {
                    lRecipes[r] = enum_RecipeStatus.Saved;
                }

                return lRecipes;
            }
        
        
        }



        


        public ITab_RecipeHolder()
        {
            this.size = WinSize;
            this.labelKey = "DB";
        }
        private Vector2 scrollPos;
        protected override void FillTab()
        {

            Listing_Standard list = new Listing_Standard();
            Rect inRect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
            Rect rect;
            Rect rect2;
            list.Begin(inRect);
            list.Gap();
            int currY = 0;
            var outRect = new Rect(5f, 5, WinSize.x - 80, WinSize.y - 200);
            var viewRect = new Rect(0f, 0, outRect.width - 16f, (itemcount + 1) * 30);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect,true);

            currY = 0;


            foreach (RecipeDef recipe in Recipes.Keys)
            {
                rect = new Rect(0, currY, viewRect.width, 30);
                currY += 30;

                //Display Image of Product
                if (recipe.products.Count > 0)
                {
                    rect2 = rect;
                    rect2.width = 30;
                    rect2.height = 30;
                    Widgets.DefIcon(rect2, recipe.products[0].thingDef);
                       
                    
                }
                
                
                rect.x = 60;
                Widgets.Label(rect, "" + recipe.label);

                rect.width = 100;
                rect.x = 350;
                

                if (Recipes[recipe] == enum_RecipeStatus.Learnable)
                {
                    if(Widgets.ButtonText(rect, "Learn"))
                    {
                        parrentDB.Quered_Recipes.Add(recipe);

                    }
                }
                else if (Recipes[recipe] == enum_RecipeStatus.Quered)
                {
                    if(Widgets.ButtonText(rect, "Cancel"))
                    {
                        if (parrentDB.Quered_Recipes.Contains(recipe))
                        {
                            parrentDB.Quered_Recipes.Remove(recipe);
                        }
                        
                    }

                }
                else if (Recipes[recipe] == enum_RecipeStatus.Saved)
                {
                    //TODO add a Are you sure? popup
                    if (Widgets.ButtonText(rect, "Forget"))
                    {
                        parrentDB.Saved_Recipes.Remove(recipe);
                    }
                }
                else if (Recipes[recipe] == enum_RecipeStatus.InPorogress)
                {
                    //TODO add a Are you sure? popup
                    if (Widgets.ButtonText(rect,  "(" + parrentDB.Progress_Learning.ToStringWorkAmount() + ") Abort"))
                    {
                        parrentDB.Recipe_Learning = null;
                    }
                }

                

            }

            Widgets.EndScrollView();


            list.End();
        }




    }
}
