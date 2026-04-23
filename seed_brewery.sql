-- ============================================================
-- BREWERY ERP — Seed Script (відповідає реальній схемі БД)
-- ============================================================
SET FOREIGN_KEY_CHECKS = 0;

-- ── beer_styles ─────────────────────────────────────────────
INSERT IGNORE INTO `beer_styles` (style_id, name, Description, MinAbv, MaxAbv, MinIbu, MaxIbu, MinSrm, MaxSrm) VALUES
(1,  'American IPA',     'Американський IPA з виразним хмелевим ароматом', 6.0, 7.5, 40, 70, 6, 14),
(2,  'Weizen',           'Баварське пшеничне пиво з нотками банана',        4.5, 5.5, 10, 20, 3, 9),
(3,  'Stout',            'Темне ірландське пиво з кавовим смаком',          4.0, 7.0, 35, 60,30, 40),
(4,  'Pilsner',          'Чеський золотий лагер',                           4.5, 5.5, 30, 45, 2, 4),
(5,  'Belgian Witbier',  'Бельгійське біле пиво з коріандром',              4.5, 5.5, 10, 20, 2, 4),
(6,  'Porter',           'Темний британський ель з шоколадом',              4.5, 6.5, 20, 40,20, 30),
(7,  'Saison',           'Бельгійський фармерський ель, сухий і пряний',   5.0, 7.0, 20, 35, 4, 8),
(8,  'New England IPA',  'Каламутний NEIPA з тропічними нотками',           6.0, 8.0, 25, 50, 3, 7),
(9,  'Lager',            'Класичний світлий лагер',                         4.0, 5.0, 10, 25, 2, 4),
(10, 'Amber Ale',        'Янтарний ель із карамельним солодом',             4.5, 6.0, 20, 40,11, 18);

-- ── ingredients ─────────────────────────────────────────────
INSERT IGNORE INTO `ingredients` (ingredient_id, name, type, total_stock, unit) VALUES
(1,  'Pilsner Malt',       'Malt',     1200.0, 'kg'),
(2,  'Munich Malt',        'Malt',      800.0, 'kg'),
(3,  'Wheat Malt',         'Malt',      600.0, 'kg'),
(4,  'Crystal 60L',        'Malt',      300.0, 'kg'),
(5,  'Chocolate Malt',     'Malt',      200.0, 'kg'),
(6,  'Roasted Barley',     'Malt',      150.0, 'kg'),
(7,  'Cascade Hops',       'Hop',        80.0, 'kg'),
(8,  'Centennial Hops',    'Hop',        60.0, 'kg'),
(9,  'Citra Hops',         'Hop',        50.0, 'kg'),
(10, 'Saaz Hops',          'Hop',       100.0, 'kg'),
(11, 'Hallertau Hops',     'Hop',        70.0, 'kg'),
(12, 'US-05 Yeast',        'Yeast',      30.0, 'pcs'),
(13, 'WB-06 Yeast',        'Yeast',      25.0, 'pcs'),
(14, 'WY1084 Irish Stout', 'Yeast',      20.0, 'pcs'),
(15, 'Saflager W-34/70',   'Yeast',      40.0, 'pcs'),
(16, 'Water Brewing',      'Water',   10000.0, 'L'),
(17, 'Coriander Seeds',    'Additive',   10.0, 'kg'),
(18, 'Orange Peel',        'Additive',    8.0, 'kg'),
(19, 'Lactose',            'Additive',   50.0, 'kg'),
(20, 'Irish Moss',         'Additive',    5.0, 'kg');

-- ── suppliers ───────────────────────────────────────────────
INSERT IGNORE INTO `suppliers` (supplier_id, name, edrpou) VALUES
(1, 'Malteurop Ukraine',  '12345678'),
(2, 'Hopsteiner GmbH',    NULL),
(3, 'Fermentis Yeast Co', '87654321'),
(4, 'БарТрейд Хоп',       '11223344'),
(5, 'AgroBrew',           '55667788');

-- ── recipes ─────────────────────────────────────────────────
INSERT IGNORE INTO `recipes` (recipe_id, style_id, version_name, is_active) VALUES
(1, 1, 'Hop Rocket IPA v1.0',     1),
(2, 2, 'Weizen Sun v2.1',         1),
(3, 3, 'Dark Matter Stout v1.5',  1),
(4, 4, 'Pilsner Premium v3.0',    1),
(5, 5, 'White Mist Witbier v1.0', 1),
(6, 6, 'Night Porter v2.0',       1),
(7, 8, 'Tropical NEIPA v1.2',     1),
(8, 9, 'Classic Lager v1.0',      1);

-- ── recipe_items ────────────────────────────────────────────
INSERT IGNORE INTO `recipe_items` (recipe_id, ingredient_id, amount) VALUES
-- IPA
(1,1,80),(1,4,10),(1,7,5),(1,8,5),(1,9,3),(1,12,2),(1,16,400),
-- Weizen
(2,3,60),(2,1,40),(2,11,2),(2,13,2),(2,16,400),
-- Stout
(3,1,60),(3,2,15),(3,5,10),(3,6,8),(3,11,3),(3,14,2),(3,16,400),
-- Pilsner
(4,1,90),(4,10,4),(4,15,2),(4,16,450),
-- Witbier
(5,1,40),(5,3,35),(5,11,2),(5,17,0.5),(5,18,0.3),(5,13,2),(5,16,400),
-- Porter
(6,1,55),(6,2,15),(6,5,8),(6,7,3),(6,14,2),(6,20,0.5),(6,16,380),
-- NEIPA
(7,1,70),(7,3,20),(7,9,8),(7,8,6),(7,19,5),(7,12,2),(7,16,400),
-- Lager
(8,1,85),(8,10,5),(8,15,2),(8,16,440);

-- ── batches ─────────────────────────────────────────────────
INSERT IGNORE INTO `batches` (batch_id, recipe_id, status, start_date, actual_abv, actual_srm) VALUES
(1, 1, 'Completed',  '2024-11-05 08:00:00', 6.8,  10),
(2, 2, 'Completed',  '2024-11-10 08:00:00', 5.1,  5),
(3, 3, 'Completed',  '2024-11-15 08:00:00', 5.5,  35),
(4, 4, 'Completed',  '2024-11-20 08:00:00', 5.0,  3),
(5, 5, 'Completed',  '2024-11-25 08:00:00', 4.8,  3),
(6, 6, 'Completed',  '2024-12-01 08:00:00', 5.8,  25),
(7, 7, 'Completed',  '2024-12-05 08:00:00', 6.5,  5),
(8, 8, 'Completed',  '2024-12-10 08:00:00', 4.5,  3),
(9, 1, 'Fermenting', '2025-01-08 08:00:00', NULL, NULL),
(10,4, 'Brewing',    '2025-01-10 08:00:00', NULL, NULL);

-- ── product_skus ────────────────────────────────────────────
INSERT IGNORE INTO `product_skus` (sku_id, batch_id, packaging_type, price, quantity_in_stock) VALUES
(1,  1, 'Keg_30L',      2800.00, 15),
(2,  1, 'Keg_50L',      4500.00, 8),
(3,  1, 'Bottle_0_5L',    85.00, 500),
(4,  2, 'Keg_30L',      2400.00, 12),
(5,  2, 'Bottle_0_5L',    72.00, 400),
(6,  3, 'Keg_30L',      2600.00, 10),
(7,  3, 'Keg_50L',      4200.00, 5),
(8,  3, 'Bottle_0_5L',    78.00, 300),
(9,  4, 'Keg_30L',      2200.00, 20),
(10, 4, 'Keg_50L',      3600.00, 10),
(11, 4, 'Bottle_0_5L',    65.00, 600),
(12, 5, 'Keg_30L',      2500.00, 8),
(13, 5, 'Bottle_0_5L',    74.00, 250),
(14, 6, 'Keg_30L',      2700.00, 6),
(15, 6, 'Bottle_0_5L',    79.00, 200),
(16, 7, 'Keg_30L',      3100.00, 10),
(17, 7, 'Bottle_0_5L',    95.00, 350),
(18, 8, 'Keg_30L',      2100.00, 18),
(19, 8, 'Keg_50L',      3400.00, 7),
(20, 8, 'Bottle_0_5L',    62.00, 700);

-- ── clients ─────────────────────────────────────────────────
INSERT IGNORE INTO `clients` (client_id, name, phone) VALUES
(1,  'Пивна Хата',       '+380667001001'),
(2,  'Ресторан Бочка',   '+380967002002'),
(3,  'Craft Bar Lviv',   '+380507003003'),
(4,  'Магазин Пивовар',  '+380447004004'),
(5,  'Пабло Паб',        '+380997005005'),
(6,  'HopYard Kyiv',     '+380737006006'),
(7,  'Ресторан Гетьман', '+380631007007'),
(8,  'Пивний Базар',     '+380671008008'),
(9,  'FreshBrew Shop',   '+380991009009'),
(10, 'Корчма Карпати',   '+380501010010');

-- ── supply_invoices ─────────────────────────────────────────
INSERT IGNORE INTO `supply_invoices` (invoice_id, supplier_id, doc_number, receive_date) VALUES
(1, 1, 'INV-2024-001', '2024-10-01 09:00:00'),
(2, 2, 'INV-2024-002', '2024-10-05 10:00:00'),
(3, 3, 'INV-2024-003', '2024-10-10 11:00:00'),
(4, 4, 'INV-2024-004', '2024-10-15 09:30:00'),
(5, 5, 'INV-2024-005', '2024-11-01 08:00:00');

-- ── invoice_items ────────────────────────────────────────────
INSERT IGNORE INTO `invoice_items` (invoice_id, ingredient_id, quantity, unit_price, expiration_date) VALUES
(1,1,500,18.50,'2025-12-01'),(1,2,300,22.00,'2025-12-01'),(1,3,200,20.00,'2025-12-01'),
(1,4,100,25.00,'2025-06-01'),(1,5,80,30.00,'2025-06-01'),(1,6,60,28.00,'2025-06-01'),
(2,7,30,180.00,'2026-01-01'),(2,8,25,195.00,'2026-01-01'),(2,9,20,210.00,'2026-01-01'),
(2,10,40,160.00,'2026-01-01'),(2,11,30,170.00,'2026-01-01'),
(3,12,15,85.00,'2025-03-01'),(3,13,12,80.00,'2025-03-01'),
(3,14,10,90.00,'2025-03-01'),(3,15,20,75.00,'2025-03-01'),
(4,7,20,182.00,'2026-02-01'),(4,8,15,198.00,'2026-02-01'),(4,9,15,212.00,'2026-02-01'),
(5,17,5,45.00,'2025-09-01'),(5,18,4,55.00,'2025-09-01'),
(5,19,25,35.00,'2026-01-01'),(5,20,3,60.00,'2025-09-01');

-- ── sales_orders ────────────────────────────────────────────
INSERT IGNORE INTO `sales_orders` (order_id, client_id, order_date, status) VALUES
(1,  1, '2024-12-01 10:00:00', 'Paid'),
(2,  2, '2024-12-03 11:00:00', 'Paid'),
(3,  3, '2024-12-05 09:00:00', 'Shipped'),
(4,  4, '2024-12-07 14:00:00', 'Shipped'),
(5,  5, '2024-12-10 10:00:00', 'Reserved'),
(6,  6, '2024-12-12 11:30:00', 'New'),
(7,  7, '2024-12-14 09:00:00', 'Paid'),
(8,  8, '2024-12-16 16:00:00', 'Reserved'),
(9,  9, '2024-12-18 10:00:00', 'Shipped'),
(10,10, '2024-12-20 13:00:00', 'New');

-- ── order_items ──────────────────────────────────────────────
INSERT IGNORE INTO `order_items` (order_id, sku_id, quantity, price_at_moment) VALUES
(1, 1,  3, 2800.00),(1, 3,100,  85.00),
(2, 9,  2, 2200.00),(2,11,200,  65.00),(2, 4, 1,2400.00),
(3,16,  2, 3100.00),(3,17, 50,  95.00),
(4, 3,100,   85.00),(4, 5,100,  72.00),(4,11,100,65.00),
(5, 6,  2, 2600.00),(5, 8, 50,  78.00),
(6, 2,  1, 4500.00),(6, 1,  2,2800.00),
(7, 9,  3, 2200.00),(7,10,  2,3600.00),
(8, 3, 50,   85.00),(8, 8, 50,  78.00),(8,17,60,95.00),
(9,18,  3, 2100.00),(9,11,100,  65.00),
(10,12, 2, 2500.00),(10,13,50,  74.00);

SET FOREIGN_KEY_CHECKS = 1;

SELECT 'Seed OK!' AS result,
  (SELECT COUNT(*) FROM beer_styles)     AS beer_styles,
  (SELECT COUNT(*) FROM ingredients)     AS ingredients,
  (SELECT COUNT(*) FROM suppliers)       AS suppliers,
  (SELECT COUNT(*) FROM recipes)         AS recipes,
  (SELECT COUNT(*) FROM batches)         AS batches,
  (SELECT COUNT(*) FROM product_skus)    AS product_skus,
  (SELECT COUNT(*) FROM clients)         AS clients,
  (SELECT COUNT(*) FROM sales_orders)    AS sales_orders,
  (SELECT COUNT(*) FROM supply_invoices) AS supply_invoices;
