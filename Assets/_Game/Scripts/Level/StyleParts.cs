using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A thing with a model of its own in one visual style (the hostage's cage in the storybook style): in that style its
    /// <see cref="Own"/> parts show and the <see cref="Default"/> ones hide, in every other style the other way round.
    /// Looks only — colliders and gameplay stay on the object itself. <see cref="VisualStyles"/> switches them all with the style.
    /// </summary>
    public class StyleParts : MonoBehaviour
    {
        [Tooltip("The style (asset name, e.g. S06_Storybook) that has its own model")]
        public string Style;
        [Tooltip("In a house furnished with models of its own (the abandoned facility) the Default parts show: its furniture takes their place")]
        public bool NotWhereFurnished;
        public GameObject[] Own = new GameObject[0];
        public GameObject[] Default = new GameObject[0];

        public static readonly List<StyleParts> All = new();

        void OnEnable()
        {
            All.Add(this);
            Show(VisualStyles.Current != null ? VisualStyles.Current.name : null);
        }

        void OnDisable() => All.Remove(this);

        /// <summary>Shows the parts for the style named <paramref name="style"/> (null = the base look).</summary>
        public void Show(string style)
        {
            bool own = style != null && style == Style && !(NotWhereFurnished && VisualStyles.HouseFamily != Stealth.Configs.ModelFamily.None);
            foreach (var go in Own)
                if (go != null && go.activeSelf != own) go.SetActive(own);
            foreach (var go in Default)
                if (go != null && go.activeSelf == own) go.SetActive(!own);
        }

        /// <summary>Every such thing in the scene to the current style.</summary>
        public static void ShowAll(string style)
        {
            foreach (var p in All) p.Show(style);
        }
    }
}
