using UnityEngine;
namespace DogLife {
public sealed class RunWeaponCatalog:ScriptableObject {
public InventoryWeapon[] weapons;
public InventoryWeapon Find(string id)=>System.Array.Find(weapons,w=>w&&w.weaponId==id);
}}
