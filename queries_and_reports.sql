-- ============================================================
-- BREWERY ERP — Аналітичні запити та звіти (Етап 6)
-- База: craft_brewery (MySQL 8+)
-- ============================================================

-- ──────────────────────────────────────────────────────────────
-- VIEW 1: Поточні залишки складу сировини з вартістю
-- ──────────────────────────────────────────────────────────────
CREATE OR REPLACE VIEW v_ingredient_stock AS
SELECT
    i.ingredient_id,
    i.name                                      AS ingredient_name,
    i.type                                      AS ingredient_type,
    i.total_stock,
    i.unit,
    i.average_cost,
    ROUND(i.total_stock * i.average_cost, 2)    AS stock_value
FROM ingredients i
ORDER BY i.type, i.name;

-- ──────────────────────────────────────────────────────────────
-- VIEW 2: Зведений звіт по замовленнях із рентабельністю
-- ──────────────────────────────────────────────────────────────
CREATE OR REPLACE VIEW v_order_profitability AS
SELECT
    so.order_id,
    c.name                                                          AS client_name,
    so.order_date,
    so.status,
    SUM(oi.quantity * oi.price_at_moment)                          AS total_revenue,
    SUM(oi.quantity * ps.unit_cost)                                AS total_cost,
    SUM(oi.quantity * oi.price_at_moment) -
        SUM(oi.quantity * ps.unit_cost)                            AS profit,
    ROUND(
        (SUM(oi.quantity * oi.price_at_moment) -
         SUM(oi.quantity * ps.unit_cost))
        / NULLIF(SUM(oi.quantity * oi.price_at_moment), 0) * 100
    , 2)                                                           AS margin_pct
FROM sales_orders  so
JOIN clients       c  ON c.client_id  = so.client_id
JOIN order_items   oi ON oi.order_id  = so.order_id
JOIN product_skus  ps ON ps.sku_id    = oi.sku_id
GROUP BY so.order_id, c.name, so.order_date, so.status;

-- ============================================================
-- ЗАПИТ 1: Топ-5 клієнтів за сумою замовлень
-- (JOIN + GROUP BY + ORDER BY + LIMIT)
-- ============================================================
SELECT
    c.client_id,
    c.name                                  AS client_name,
    COUNT(DISTINCT so.order_id)            AS total_orders,
    SUM(oi.quantity * oi.price_at_moment)  AS total_revenue
FROM clients      c
JOIN sales_orders so ON so.client_id = c.client_id
JOIN order_items  oi ON oi.order_id  = so.order_id
GROUP BY c.client_id, c.name
ORDER BY total_revenue DESC
LIMIT 5;

-- ============================================================
-- ЗАПИТ 2: Оборот по місяцях (агрегат за датою)
-- ============================================================
SELECT
    DATE_FORMAT(so.order_date, '%Y-%m')    AS month,
    COUNT(DISTINCT so.order_id)            AS orders_count,
    SUM(oi.quantity * oi.price_at_moment)  AS monthly_revenue
FROM sales_orders so
JOIN order_items  oi ON oi.order_id = so.order_id
WHERE so.status IN ('Shipped', 'Paid')
GROUP BY month
ORDER BY month DESC;

-- ============================================================
-- ЗАПИТ 3: Залишки складу готової продукції
-- (JOIN через batches → recipes → beer_styles)
-- ============================================================
SELECT
    ps.sku_id,
    bs.name                                 AS style_name,
    r.version_name                          AS recipe_version,
    b.batch_id,
    ps.packaging_type,
    ps.quantity_in_stock,
    ps.price,
    ROUND(ps.quantity_in_stock * ps.price, 2) AS stock_value
FROM product_skus ps
JOIN batches      b  ON b.batch_id   = ps.batch_id
JOIN recipes      r  ON r.recipe_id  = b.recipe_id
JOIN beer_styles  bs ON bs.style_id  = r.style_id
WHERE ps.quantity_in_stock > 0
ORDER BY stock_value DESC;

-- ============================================================
-- ЗАПИТ 4: Прибутковість партій (JOIN 4 таблиць + агрегат)
-- ============================================================
SELECT
    b.batch_id,
    bs.name                                AS style_name,
    r.version_name,
    b.status,
    b.start_date,
    b.actual_abv,
    SUM(ps.quantity_in_stock * ps.price)  AS remaining_stock_value,
    SUM(oi.quantity * oi.price_at_moment) AS realized_revenue,
    SUM(oi.quantity * ps.unit_cost)       AS realized_cost,
    SUM(oi.quantity * oi.price_at_moment)
        - SUM(oi.quantity * ps.unit_cost) AS gross_profit
FROM batches      b
JOIN recipes      r  ON r.recipe_id  = b.recipe_id
JOIN beer_styles  bs ON bs.style_id  = r.style_id
LEFT JOIN product_skus ps ON ps.batch_id = b.batch_id
LEFT JOIN order_items  oi ON oi.sku_id   = ps.sku_id
GROUP BY b.batch_id, bs.name, r.version_name, b.status, b.start_date, b.actual_abv
ORDER BY b.batch_id;

-- ============================================================
-- ЗАПИТ 5: Найпопулярніші SKU (кількість замовлень)
-- ============================================================
SELECT
    ps.sku_id,
    bs.name                                 AS style_name,
    ps.packaging_type,
    COUNT(oi.order_id)                      AS times_ordered,
    SUM(oi.quantity)                        AS total_qty_sold,
    SUM(oi.quantity * oi.price_at_moment)   AS total_revenue
FROM order_items  oi
JOIN product_skus ps ON ps.sku_id   = oi.sku_id
JOIN batches      b  ON b.batch_id  = ps.batch_id
JOIN recipes      r  ON r.recipe_id = b.recipe_id
JOIN beer_styles  bs ON bs.style_id = r.style_id
GROUP BY ps.sku_id, bs.name, ps.packaging_type
ORDER BY total_qty_sold DESC
LIMIT 10;

-- ============================================================
-- ЗАПИТ 6: Витрати сировини на виробництво (GROUP BY тип)
-- ============================================================
SELECT
    i.type                              AS ingredient_type,
    COUNT(DISTINCT i.ingredient_id)     AS ingredient_count,
    SUM(ri.amount)                      AS total_used_in_recipes,
    i.unit,
    ROUND(SUM(ri.amount * i.average_cost), 2) AS estimated_material_cost
FROM recipe_items ri
JOIN ingredients  i  ON i.ingredient_id = ri.ingredient_id
GROUP BY i.type, i.unit
ORDER BY estimated_material_cost DESC;

-- ============================================================
-- ЗАПИТ 7: Постачальники — обсяг поставок та середня ціна
-- ============================================================
SELECT
    s.supplier_id,
    s.name                                      AS supplier_name,
    COUNT(DISTINCT si.invoice_id)               AS total_invoices,
    SUM(ii.quantity)                            AS total_qty_supplied,
    ROUND(AVG(ii.unit_price), 2)               AS avg_unit_price,
    ROUND(SUM(ii.quantity * ii.unit_price), 2) AS total_supply_value
FROM suppliers      s
JOIN supply_invoices si ON si.supplier_id  = s.supplier_id
JOIN invoice_items  ii ON ii.invoice_id    = si.invoice_id
WHERE ii.unit_price IS NOT NULL
GROUP BY s.supplier_id, s.name
ORDER BY total_supply_value DESC;

-- ============================================================
-- ЗАПИТ 8: Структурна перевірка — рецепти без інгредієнтів
-- та інгредієнти без постачань (перевірка цілісності даних)
-- ============================================================
-- 8a. Активні рецепти без жодного інгредієнта
SELECT r.recipe_id, r.version_name, bs.name AS style
FROM recipes     r
JOIN beer_styles bs ON bs.style_id = r.style_id
WHERE r.is_active = 1
  AND NOT EXISTS (
      SELECT 1 FROM recipe_items ri WHERE ri.recipe_id = r.recipe_id
  );

-- 8b. Інгредієнти на нулі запасів (потребують завезення)
SELECT
    i.ingredient_id,
    i.name,
    i.type,
    i.total_stock,
    i.unit
FROM ingredients i
WHERE i.total_stock <= 0
ORDER BY i.type, i.name;
