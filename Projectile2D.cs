using System;
using System.Collections.Generic;
using UnityEngine;

public class Projectile2D : MonoBehaviour
{
    public SpriteRenderer render;
    public Rigidbody2D rb;
    public Collider2D col;
    public ContactFilter2D filter;

    public Action<Transform, Projectile2D> OnHit = null;
    public List<Transform> targets = new();
    Collider2D[] results;
    bool hitted;
    public float LifeTime;
    public float time;
    
    void Awake()
    {
        render = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    // Update is called once per frame
    void Update()
    {
        results = new Collider2D[10];
        targets.Clear();

        int count = Physics2D.OverlapCollider(col, filter, results);

        if (count > 0) {
            for (int i = 0; i < count; i++) {
                Collider2D col_ = results[i];
                if ((LayerMask.GetMask("ground") & (1 << col_.gameObject.layer)) != 0) {
                    continue;
                }

                targets.Add(col_.transform);
            }
        }

        if (hitted) {
            if (targets.Count <= 0) {
                hitted = false;
            }
        } else {
            if (targets.Count > 0) {
                if (OnHit != null) {
                    OnHit(targets[0], this);
                }

                hitted = true;
            }
        }

        if (LifeTime != 0) {
            time += Time.deltaTime;

            if (time > LifeTime) {
                Destroy(gameObject);
            }
        }
    }
}