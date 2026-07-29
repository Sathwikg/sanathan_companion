using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanathana.Companion.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds the eight chant categories and two authentic chants under each.
    /// Data-only: idempotent (ON CONFLICT DO NOTHING) and id-agnostic - categories and
    /// deities are resolved by name, so it applies cleanly to an existing database as
    /// well as a freshly created one, and never overwrites edits made by users.
    /// </summary>
    public partial class SeedChantData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SeedSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UnseedSql);
        }

        private const string SeedSql = """
-- Chant categories. Fixed ids so a rebuilt database reproduces them; ON CONFLICT keeps
-- whatever ids an existing database already has.
INSERT INTO "Chants" ("Id","Name","Description","HasCount","Count","IsActive","CreatedBy","CreatedDate")
VALUES ('ca000000-0000-0000-0000-000000000001'::uuid, 'Stotra', 'A devotional hymn composed in praise of a deity, glorifying its qualities and deeds, with no fixed number of verses.', false, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "Chants" ("Id","Name","Description","HasCount","Count","IsActive","CreatedBy","CreatedDate")
VALUES ('ca000000-0000-0000-0000-000000000002'::uuid, 'Ashtakam', 'A devotional hymn composed of eight verses in praise of a deity (from Sanskrit ashta, meaning eight).', true, 8, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "Chants" ("Id","Name","Description","HasCount","Count","IsActive","CreatedBy","CreatedDate")
VALUES ('ca000000-0000-0000-0000-000000000003'::uuid, 'Ashtottaram', 'A devotional litany of 108 sacred names of a deity, each recited in praise and often accompanied by offerings.', true, 108, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "Chants" ("Id","Name","Description","HasCount","Count","IsActive","CreatedBy","CreatedDate")
VALUES ('ca000000-0000-0000-0000-000000000004'::uuid, 'Chalisa', 'A forty-verse devotional hymn in praise of a deity, most famously the Hanuman Chalisa.', true, 40, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "Chants" ("Id","Name","Description","HasCount","Count","IsActive","CreatedBy","CreatedDate")
VALUES ('ca000000-0000-0000-0000-000000000005'::uuid, 'Sahasranama', 'A devotional hymn that venerates a deity through a litany of one thousand sacred names, each evoking a distinct attribute or aspect of the divine.', true, 1000, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "Chants" ("Id","Name","Description","HasCount","Count","IsActive","CreatedBy","CreatedDate")
VALUES ('ca000000-0000-0000-0000-000000000006'::uuid, 'Shloka', 'A Sanskrit verse or couplet, traditionally in the anushtubh meter, conveying a devotional, philosophical, or instructional idea.', false, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "Chants" ("Id","Name","Description","HasCount","Count","IsActive","CreatedBy","CreatedDate")
VALUES ('ca000000-0000-0000-0000-000000000007'::uuid, 'Mantra', 'A sacred sound, syllable, word, or formula repeated in prayer or meditation to focus the mind and invoke the divine.', false, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "Chants" ("Id","Name","Description","HasCount","Count","IsActive","CreatedBy","CreatedDate")
VALUES ('ca000000-0000-0000-0000-000000000008'::uuid, 'Prayer', 'A heartfelt invocation or supplication offered to the divine, expressing devotion, gratitude, or a request for grace and blessings.', false, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00')
ON CONFLICT ("Name") DO NOTHING;

-- Two chants per category. ChantId and DeityIds resolve by NAME so the seed works
-- regardless of the ids the target database happens to hold.
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000001'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Stotra'), 'Sankata Nashana Ganesha Stotram', 'A hymn from the Narada Purana, spoken by Sage Narada, invoking twelve names of Ganesha to destroy troubles and obstacles.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Ganesha')), '<h3>Sankata Nashana Ganesha Stotram</h3><p>pranamya sirasa devam gauriputram vinayakam |<br>bhaktavasam smarennityam ayuhkamarthasiddhaye ||</p><p>prathamam vakratundam ca ekadantam dvitiyakam |<br>tritiyam krishnapingaksham gajavaktram caturthakam ||</p><blockquote><em>Bowing my head to the Lord Vinayaka, son of Gauri, who dwells with his devotees, I remember him daily for long life, fulfilment of desires, prosperity and success. First is Vakratunda, second Ekadanta, third Krishnapingaksha, fourth Gajavaktra.</em></blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Stotra')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000002'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Stotra'), 'Kanakadhara Stotram', 'A hymn to Goddess Lakshmi composed by Adi Shankaracharya, traditionally recited to invoke prosperity and abundance.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Lakshmi')), '<h3>Kanakadhara Stotram</h3><p>angam hare pulaka bhushanam ashrayanti<br>bhringanganeva mukulabharanam tamalam<br>angikritakhila vibhutir apanga leela<br>mangalyadastu mama mangala devatayah</p><p>mugdha muhur vidadhati vadane murareh<br>prematrapa pranihitani gatagatani<br>mala drishor madhukareeva mahotpale ya<br>sa me shriyam dishatu sagara sambhavayah</p><blockquote>May she who rests upon the thrilled form of Hari as a bee upon the budding tamala tree, whose shy and playful glances move again and again over the face of Murari like a bee over a lotus, grant me all auspiciousness and prosperity.</blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Stotra')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000003'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Ashtakam'), 'Achyutashtakam', 'An eight-verse hymn attributed to Adi Shankaracharya praising Vishnu through a garland of his names such as Achyuta, Keshava and Krishna.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Vishnu')), '<h3>Achyutashtakam</h3><p>Achyutam keshavam rama narayanam<br>Krishna damodaram vasudevam harim<br>Sridharam madhavam gopika vallabham<br>Janaki nayakam ramachandram bhaje</p><p>Achyutam keshavam satyabhamadhavam<br>Madhavam sridharam radhikaradhitam<br>Indira mandiram chetasa sundaram<br>Devaki nandanam nandajam sandadhe</p><blockquote>I worship Achyuta, Keshava, Rama, Narayana, Krishna, Damodara, Vasudeva and Hari; the bearer of Sri, Madhava, the beloved of the gopis, the lord of Janaki, Ramachandra. I hold in my heart Achyuta, Keshava, the lord of Satyabhama, Madhava, Sridhara, the one adored by Radhika, the abode of Lakshmi, beautiful to contemplate, the delight of Devaki and the son of Nanda.</blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Ashtakam')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000004'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Ashtakam'), 'Madhurashtakam', 'An eight-verse Sanskrit hymn by Sri Vallabhacharya praising the all-pervading sweetness of Lord Krishna''s form, deeds and being.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Vishnu')), '<h3>Madhurashtakam</h3><p>adharam madhuram vadanam madhuram<br>nayanam madhuram hasitam madhuram<br>hridayam madhuram gamanam madhuram<br>madhuradhipater akhilam madhuram</p><p>vachanam madhuram charitam madhuram<br>vasanam madhuram valitam madhuram<br>chalitam madhuram bhramitam madhuram<br>madhuradhipater akhilam madhuram</p><blockquote>Sweet are His lips, sweet His face, sweet His eyes, sweet His smile, sweet His heart, sweet His gait; everything about the Lord of sweetness is sweet. Sweet is His speech, sweet His conduct, sweet His garment, sweet His graceful bearing, sweet His movement, sweet His wandering; everything about the Lord of sweetness is sweet.</blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Ashtakam')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000005'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Ashtottaram'), 'Ganesha Ashtottara Shatanamavali', 'Traditional litany of the 108 sacred names of Lord Ganesha, chanted name by name to invoke his grace and remove obstacles.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Ganesha')), '<h3>Ganesha Ashtottara Shatanamavali</h3><p>Om Gajananaya namah<br>Om Ganadhyakshaya namah<br>Om Vighnarajaya namah<br>Om Vinayakaya namah<br>Om Dvaimaturaya namah<br>Om Dvimukhaya namah<br>Om Pramukhaya namah<br>Om Sumukhaya namah<br>Om Krutine namah<br>Om Supradipaya namah</p><blockquote>Salutations to the elephant-faced one, to the lord of the ganas, to the sovereign over all obstacles, to the supreme guide, to him born of two mothers, to the two-faced one, to the foremost one, to the gracious-faced one, to the doer of all deeds, to the shining lamp.</blockquote><p><em>(Opening names only; the recitation continues to 108.)</em></p>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Ashtottaram')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000006'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Ashtottaram'), 'Lakshmi Ashtottara Shatanamavali', 'A traditional litany of the 108 names of Goddess Lakshmi, drawn from the Lakshmi Ashtottara Shatanama Stotram and offered during her puja.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Lakshmi')), '<h3>Lakshmi Ashtottara Shatanamavali</h3><p>Om Prakrityai Namah<br>Om Vikrityai Namah<br>Om Vidyayai Namah<br>Om Sarvabhutahitapradayai Namah<br>Om Shraddhayai Namah<br>Om Vibhutyai Namah<br>Om Surabhyai Namah<br>Om Paramatmikayai Namah<br>Om Vache Namah<br>Om Padmalayayai Namah</p><blockquote>Salutations to her who is Nature itself, who is its transformation, who is Knowledge, who grants welfare to all beings, who is Faith, who is Abundance, who is the wish-granting Surabhi, who is the Supreme Self, who is Speech, and who dwells upon the lotus.<br><em>These are the first ten of the 108 names; the archana continues through the full list.</em></blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Ashtottaram')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000007'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Chalisa'), 'Hanuman Chalisa', 'A forty-verse devotional hymn in praise of Hanuman, composed in Awadhi by the 16th-century saint-poet Goswami Tulsidas.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Hanuman')), '<h3>Hanuman Chalisa - Opening Dohas and First Chaupais</h3><p>Shri Guru Charan Saroj Raj,<br>Nija Manu Mukuru Sudhari.<br>Baranau Raghubar Bimal Jasu,<br>Jo Dayaku Phal Chari.</p><p>Buddhi Heen Tanu Janike,<br>Sumirau Pavan Kumar.<br>Bal Buddhi Vidya Dehu Mohi,<br>Harahu Kalesa Bikar.</p><p>Jai Hanuman Gyan Gun Sagar,<br>Jai Kapis Tihun Lok Ujagar.<br>Ram Doot Atulit Bal Dhama,<br>Anjani Putra Pavan Sut Nama.</p><blockquote>Cleansing the mirror of my mind with the dust of my Guru''s lotus feet, I sing the pure glory of Raghubar, who bestows the four fruits of life. Knowing my body to be devoid of wisdom, I remember the Son of the Wind: grant me strength, intelligence and knowledge, and remove my afflictions and impurities. Victory to Hanuman, ocean of wisdom and virtue; victory to the Lord of the monkeys who illumines the three worlds. You are Rama''s messenger, abode of immeasurable strength, known as the son of Anjani and the son of the Wind.</blockquote>', TIME '18:00:00', TIME '19:30:00', 'Evening Prayer', true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Chalisa')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000008'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Chalisa'), 'Shri Ganesh Chalisa', 'A traditional forty-verse Hindi hymn praising Lord Ganesha as remover of obstacles and giver of wisdom, recited before new undertakings.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Ganesha')), '<h3>Doha</h3><p>Jai Ganapati Sadguna Sadan, Kavi Var Badan Kripal<br>Vighna Haran Mangal Karan, Jai Jai Girijalal</p><h3>Chaupai</h3><p>Jai Jai Jai Ganapati Gan Raju, Mangal Bharan Karan Shubh Kaju<br>Jai Gajbadan Sadan Sukhdata, Vishwa Vinayak Buddhi Vidhata</p><blockquote>Victory to Ganapati, abode of all virtues, gracious upon the best of poets; remover of obstacles and bringer of auspiciousness, hail to the son of Girija. Hail lord of the ganas, who fills works with blessing and brings good deeds to fruition; hail elephant-faced giver of joy, leader of the universe and dispenser of wisdom.</blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Chalisa')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000009'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Sahasranama'), 'Vishnu Sahasranama', 'The thousand names of Lord Vishnu, taught by Bhishma to Yudhishthira in the Anushasana Parva of Vyasa''s Mahabharata.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Vishnu')), '<h3>Vishnu Sahasranama - Opening Verses</h3><p>Om vishvam vishnur vashatkaro bhuta-bhavya-bhavat-prabhuh |<br>bhuta-krid bhuta-bhrid bhavo bhutatma bhuta-bhavanah ||<br>putatma paramatma cha muktanam parama gatih |<br>avyayah purushah sakshi kshetrajno ''kshara eva cha ||</p><blockquote><em>He is the universe itself, the all-pervading Vishnu, the lord of the sacrificial call, master of past, present and future; the maker, sustainer and inner Self of all beings; the pure Self, the supreme Self, and the highest goal of the liberated.</em></blockquote><p><b>Note:</b> These are only the first two verses; the hymn continues with all one thousand names.</p>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Sahasranama')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000010'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Sahasranama'), 'Lakshmi Sahasranama Stotram', 'Skanda Purana hymn of the 1008 names of Goddess Mahalakshmi, taught by Sage Sanatkumara to the yogis and related by Sage Gargya.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Lakshmi')), '<h3>Sri Lakshmi Sahasranama Stotram</h3><p><em>The sages ask Gargya for the thousand and eight names</em><br>namnam sashtasahasram cha bruhi gargya mahamate |<br>mahalakshmya mahadevya bhuktimuktyartha siddhaye ||</p><p><em>The naming begins</em><br>nityagatanantanitya nandini janaranjani |<br>nityaprakashini chaiva svaprakashasvarupini ||</p><blockquote>O greatly wise Gargya, tell us the thousand and eight names of Mahalakshmi, the great Goddess, for the gaining of both enjoyment and liberation. She is the ever-attained, endless and eternal; the joy-giver who delights all beings; ever-shining, she is luminous by her own light alone.</blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Sahasranama')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000011'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Shloka'), 'Vakratunda Mahakaya', 'A classic Sanskrit dhyana shloka invoking Lord Ganesha to remove obstacles, traditionally recited before beginning any new undertaking.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Ganesha')), '<h3>Vakratunda Mahakaya</h3><p>Vakratunda mahakaya suryakoti samaprabha |<br>Nirvighnam kuru me deva sarvakaryeshu sarvada ||</p><blockquote>O Lord with the curved trunk and mighty form, whose splendour equals a million suns, make all my endeavours free of obstacles, always.</blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Shloka')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000012'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Shloka'), 'Guru Vandana', 'A classic Sanskrit shloka from the Guru Gita of the Skanda Purana, saluting the Guru as Brahma, Vishnu, Shiva and the Supreme Brahman.',
       NULL, '<h3>Guru Vandana</h3><p>Gurur Brahma Gurur Vishnuh<br>Gurur Devo Maheshwarah<br>Guruh Sakshat Param Brahma<br>Tasmai Shri Gurave Namah</p><blockquote>The Guru is Brahma, the Guru is Vishnu, the Guru is Lord Maheshwara; the Guru is verily the Supreme Brahman - to that revered Guru I bow.</blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Shloka')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000013'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Mantra'), 'Om Gam Ganapataye Namaha', 'The traditional bija (seed) mantra of Ganesha, chanted before any new undertaking to invoke his grace and remove obstacles.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Ganesha')), '<h3>Ganapati Bija Mantra</h3><p>Om Gam Ganapataye Namaha</p><blockquote>Om, salutations to Ganapati, lord of the ganas; the seed syllable <em>Gam</em> invokes his presence to clear every obstacle from the path. In japa it is traditionally repeated 108 times.</blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Mantra')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000014'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Mantra'), 'Om Namo Bhagavate Vasudevaya', 'The twelve-syllable Dvadasakshari mantra of Lord Vishnu, taught by sage Narada to the boy Dhruva in the Srimad Bhagavatam.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Vishnu')), '<h3>Dvadasakshari Mantra</h3><p>Om Namo Bhagavate Vasudevaya<br>Om Namo Bhagavate Vasudevaya<br>Om Namo Bhagavate Vasudevaya</p><p><em>Srimad Bhagavatam 4.8.54</em><br>om namo bhagavate vasudevaya<br>mantrenanena devasya kuryad dravyamayim budhah<br>saparyam vividhair dravyair desa-kala-vibhagavit</p><blockquote>Om, salutations to Bhagavan Vasudeva. With this mantra the wise one, knowing the divisions of place and time, should worship the form of the Lord with varied offerings.</blockquote>', NULL, NULL, NULL, true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Mantra')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000015'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Prayer'), 'Karagre Vasate Lakshmi', 'Traditional anonymous Sanskrit Karadarshanam shloka, recited on waking while gazing at one''s own palms before rising from bed.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Lakshmi','Vishnu')), '<h3>Karadarshanam</h3><p>karagre vasate lakshmih<br>karamadhye sarasvati<br>karamule tu govindah<br>prabhate karadarshanam</p><blockquote><em>At the tips of the fingers dwells Lakshmi, in the middle of the palm dwells Sarasvati, and at the root of the palm dwells Govinda; therefore at dawn one should behold one''s own hands.</em></blockquote>', TIME '04:30:00', TIME '07:00:00', 'Morning Prayer', true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Prayer')
ON CONFLICT ("Name") DO NOTHING;
INSERT INTO "ChantConfigs" ("Id","ChantId","Name","Description","DeityIds","ChantText","FromTime","ToTime","TimeDescription","IsActive","CreatedBy","CreatedDate")
SELECT 'cc000000-0000-0000-0000-000000000016'::uuid, (SELECT "Id" FROM "Chants" WHERE "Name" = 'Prayer'), 'Brahmarpanam Brahma Havir', 'Verses from the Bhagavad Gita (4.24 and 15.14), spoken by Sri Krishna, recited as grace before meals to offer food to the Divine.',
       (SELECT string_agg("Id"::text, ',' ORDER BY "Name") FROM "Deities" WHERE "Name" IN ('Vishnu')), '<h3>Brahmarpanam - Food Prayer</h3><p>Om brahmarpanam brahma havir<br>brahmagnau brahmana hutam<br>brahmaiva tena gantavyam<br>brahma karma samadhina</p><p>Aham vaishvanaro bhutva<br>praninam deham ashritah<br>pranapana samayuktah<br>pachamy annam chaturvidham</p><blockquote>The offering is Brahman, the oblation is Brahman, poured by Brahman into the fire of Brahman; Brahman alone is reached by one absorbed in the action that is Brahman. Becoming the fire of digestion within all living beings, joined with the incoming and outgoing breath, I digest the four kinds of food.</blockquote>', TIME '12:00:00', TIME '13:30:00', 'Food Prayer', true, 'system', TIMESTAMPTZ '2026-01-01 00:00:00+00'
WHERE EXISTS (SELECT 1 FROM "Chants" WHERE "Name" = 'Prayer')
ON CONFLICT ("Name") DO NOTHING;
""";

        private const string UnseedSql = """
DELETE FROM "ChantConfigs" WHERE "Id" IN ('cc000000-0000-0000-0000-000000000001','cc000000-0000-0000-0000-000000000002','cc000000-0000-0000-0000-000000000003','cc000000-0000-0000-0000-000000000004','cc000000-0000-0000-0000-000000000005','cc000000-0000-0000-0000-000000000006','cc000000-0000-0000-0000-000000000007','cc000000-0000-0000-0000-000000000008','cc000000-0000-0000-0000-000000000009','cc000000-0000-0000-0000-000000000010','cc000000-0000-0000-0000-000000000011','cc000000-0000-0000-0000-000000000012','cc000000-0000-0000-0000-000000000013','cc000000-0000-0000-0000-000000000014','cc000000-0000-0000-0000-000000000015','cc000000-0000-0000-0000-000000000016');
DELETE FROM "Chants" WHERE "Id" IN ('ca000000-0000-0000-0000-000000000001','ca000000-0000-0000-0000-000000000002','ca000000-0000-0000-0000-000000000003','ca000000-0000-0000-0000-000000000004','ca000000-0000-0000-0000-000000000005','ca000000-0000-0000-0000-000000000006','ca000000-0000-0000-0000-000000000007','ca000000-0000-0000-0000-000000000008');
""";
    }
}
