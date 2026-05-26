using UnityEngine;
using System.Collections;

public class BulletScript : MonoBehaviour {

	[Tooltip("Furthest distance bullet will look for target")]
	public float maxDistance = 1000000;
	RaycastHit hit;
	[Tooltip("Prefab of wall damage hit. The object needs 'LevelPart' tag to create decal on it.")]
	public GameObject decalHitWall;
	[Tooltip("Decal will need to be slightly in front of the wall so it doesn't cause rendering problems so for best feel put from 0.01-0.1.")]
	public float floatInfrontOfWall;
	[Tooltip("Blood prefab particle this bullet will create upon hitting enemy")]
	public GameObject bloodEffect;
	[Tooltip("Put Weapon layer and Player layer to ignore bullet raycast.")]
	public LayerMask ignoreLayer;

	[Header("Deer Damage")]
	[Tooltip("Damage dealt to a Deer on hit.")]
	public float deerDamage = 25f;

	/*
	* Upon bullet creation with this script attached,
	* bullet creates a raycast which searches for corresponding tags.
	* If raycast finds something it will create a decal of corresponding tag.
	* Extended: also damages DeerAI components on hit.
	*/
	void Update () {

		if(Physics.Raycast(transform.position, transform.forward, out hit, maxDistance, ~ignoreLayer)){

			// ── Wall decal ──────────────────────────────────────
			if(hit.transform.tag == "LevelPart"){
				if(decalHitWall)
					Instantiate(decalHitWall, hit.point + hit.normal * floatInfrontOfWall,
					             Quaternion.LookRotation(hit.normal));
				Destroy(gameObject);
				return;
			}

			// ── Deer hit ─────────────────────────────────────────
			// Supports both "Dummie" (legacy) and "Deer" tags, and
			// any object that has DeerAI anywhere in its hierarchy.
			DeerAI deer = hit.transform.GetComponentInParent<DeerAI>()
			           ?? hit.transform.GetComponent<DeerAI>();

			if (deer != null)
			{
				if(bloodEffect)
					Instantiate(bloodEffect, hit.point, Quaternion.LookRotation(hit.normal));

				Vector3 knockDir = transform.forward;
				deer.TakeDamage(deerDamage, knockDir);
				Destroy(gameObject);
				return;
			}

			// Legacy "Dummie" tag fallback (no DeerAI)
			if(hit.transform.tag == "Dummie"){
				if(bloodEffect)
					Instantiate(bloodEffect, hit.point, Quaternion.LookRotation(hit.normal));
				Destroy(gameObject);
				return;
			}

			Destroy(gameObject);
		}
		Destroy(gameObject, 0.1f);
	}

}