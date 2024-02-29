using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PICSeL.Configs;
using PICSeL.Models;
using System.Net;
using System.Text;

namespace PICSeL.Utils
{
    public class AzureAIHelper
    {
        private OpenAIConfig _openAIConfig;
        private ILogger<AzureAIHelper> _logger;

        public AzureAIHelper(IOptions<OpenAIConfig> config, ILogger<AzureAIHelper> logger)
        {
            _openAIConfig = config.Value;
            _logger = logger;
        }       

        public string GetSumamryFromDocuments(string message, string lang = "EN")
        {
            return GetResponseFromOpenAI(message, createSummarizeRequest, lang)?.Choices?.FirstOrDefault()?.Message.content ?? string.Empty;
        }

        public string GetVideoScript(string message, bool isQuery = false)
        {
            return GetResponseFromOpenAI(message, CreateVideoScript, lang)?.Choices?.FirstOrDefault()?.Message.content ?? string.Empty;
        }

        public string GetSSMLFromScript(string message, string lang = "EN")
        {
            return GetResponseFromOpenAI(message, CreateSSMLRequest, lang)?.Choices?.FirstOrDefault()?.Message.content ?? string.Empty;
        }

        public string GetQueryAnswer(string query)
        {
            return GetResponseFromOpenAI(query, createQueryRequest)?.Choices?.FirstOrDefault()?.Message.content ?? string.Empty;
        }

        public string GetLocalizedAnswerForQuery(string query, string targetLocale, string sourceLocale)
        {
            query = $"Translate the following {sourceLocale} text to {targetLocale}: \"{query}\"'}}";
            return GetResponseFromOpenAI(query, createLocalizedQueryRequest)?.Choices?.FirstOrDefault()?.Message.content ?? string.Empty;
        }

        private OpenAIResponse? GetResponseFromOpenAI(string content, Func<string, string> getPrompt)
        { 
            try
            {

                var client = new HttpClient();

                client.DefaultRequestHeaders.Add("api-key", _openAIConfig.ApiKey);

                var request = new HttpRequestMessage(HttpMethod.Post, _openAIConfig.Uri)
                {
                    Content = new StringContent(getPrompt(content, lang), Encoding.UTF8, "application/json")
                };

                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                request.Headers.Add("Connection", "keep-alive");

                // Send the request and get the response
                var response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    // Read the response content as a string
                    string jsonResponse = response.Content.ReadAsStringAsync().Result;

                    // Deserialize the JSON response into an OpenAiResponse object
                    OpenAIResponse openAiResponse = JsonConvert.DeserializeObject<OpenAIResponse>(jsonResponse);

                    openAiResponse.Status = response.StatusCode;

                    return openAiResponse;
                }

                //Fallback to backupModel
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var clientBackup = new HttpClient();

                    clientBackup.DefaultRequestHeaders.Add("api-key", _openAIConfig.ApiKeyBackup);

                    var backupRequest = new HttpRequestMessage(HttpMethod.Post, _openAIConfig.BackupUri)
                    {
                        Content = new StringContent(getPrompt(content, lang), Encoding.UTF8, "application/json")
                    };

                    backupRequest.Headers.Add("Accept", "application/json");
                    backupRequest.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                    backupRequest.Headers.Add("Connection", "keep-alive");

                    // Send the request and get the response
                    var backupResponse = clientBackup.SendAsync(backupRequest).Result;

                    if (backupResponse.IsSuccessStatusCode)
                    {
                        // Read the response content as a string
                        string jsonResponse = backupResponse.Content.ReadAsStringAsync().Result;

                        // Deserialize the JSON response into an OpenAiResponse object
                        OpenAIResponse openAiResponse = JsonConvert.DeserializeObject<OpenAIResponse>(jsonResponse);

                        openAiResponse.Status = backupResponse.StatusCode;

                        clientBackup.Dispose();

                        return openAiResponse;
                    }

                    return new OpenAIResponse()
                    {
                        Status = backupResponse.StatusCode,
                    };

                }

                return new OpenAIResponse()
                {
                    Status = response.StatusCode,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return new OpenAIResponse()
                {
                    Status = HttpStatusCode.InternalServerError,
                };
            }

        }

        private static string createSummarizeRequest(string details, string lang = "EN")
        {

            OpenAIRequest aiRequest = new OpenAIRequest
            {
                temperature = 0.5,
                top_p = 0.5,
                frequency_penalty = 0.3,
                presence_penalty = 0.3,
                max_tokens = 4096,
                stop = null,
                stream = false,
                messages = new List<Message>
                    {
                        new Message
                        {
                            role = "system",
                            content = "You are an assistant content editor. Your responsibility is to skim through all the verbose content being provided to you and summarize it in a way that can help the creator create a short content video of around 0-5 minutes from your response."
                        },
                    }
            };

            aiRequest.messages.Add(new Message
            {
                role = "user",
                content = $"Here is the content that needs to be summarized: {details}"
            });

            return JsonConvert.SerializeObject(aiRequest);
        }

        private static string createQueryRequest(string query)
        {

            OpenAIRequest aiRequest = new OpenAIRequest
            {
                temperature = 0.5,
                top_p = 0.5,
                frequency_penalty = 0.3,
                presence_penalty = 0.3,
                max_tokens = 4096,
                stop = null,
                stream = false,
                messages = new List<Message>
                    {
                        new Message
                        {
                            role = "system",
                            content = "You are an assistant content editor. Your responsibility is to summarize answer in a way that can help the creator create a short content video of around 0-1 minute from your response."
                        },
                        new Message
                        {
                            role = "user",
                            content = query
                        },
                    }
            };

            return JsonConvert.SerializeObject(aiRequest);
        }

        private static string createLocalizedQueryRequest(string query)
        {

            OpenAIRequest aiRequest = new OpenAIRequest
            {
                temperature = 0.5,
                top_p = 0.5,
                frequency_penalty = 0.3,
                presence_penalty = 0.3,
                max_tokens = 4096,
                stop = null,
                stream = false,
                messages = new List<Message>
                    {
                        new Message
                        {
                            role = "user",
                            content = query
                        },
                    }
            };

            return JsonConvert.SerializeObject(aiRequest);
        }

        private static string CreateSSMLRequest(string script)
        {
            var trainingScript1 = "Solar Flares: Unveiling the Power of Our Sun\r\n[Scene 1: Introduction to the Sun]\r\nWelcome to our exploration of solar flares, the Sun's most captivating phenomena. The Sun, a ball of hot plasma and the heart of our solar system, is not just a source of light and warmth; it's a dynamic and turbulent star, capable of releasing incredible amounts of energy in the form of solar flares.\r\n\r\n[Cut to visual: Time-lapse of the Sun with visible solar activities]\r\nImagine watching the Sun's surface, witnessing an intense brightening, a burst of light that outshines the surrounding areas. This is the beginning of a solar flare, a spectacular display of the Sun's power.\r\n\r\n[Scene 2: What are Solar Flares?]\r\nSolar flares are sudden eruptions of energy on the Sun's surface. They result from the tangling, crossing, or reorganizing of magnetic field lines near sunspots. The energy released can be equivalent to millions of 100-megaton hydrogen bombs exploding at the same time.\r\n\r\n[Cut to visual: Animation showing magnetic field lines tangling and releasing a flare]\r\nAs these magnetic fields snap and realign, they release a massive amount of energy in the form of light, heat, and a stream of highly energetic particles. This process is what we observe as a solar flare.\r\n\r\n[Scene 3: The Impact of Solar Flares]\r\nSolar flares can have profound effects on Earth. The intense light and energetic particles can disrupt satellite operations, communication systems, and even power grids. They also contribute to the awe-inspiring natural light show known as the auroras, more commonly known as the Northern and Southern Lights.\r\n\r\n[Cut to visual: Satellite in space experiencing interference, followed by footage of auroras]\r\nThe charged particles can ionize Earth's atmosphere, interfering with radio communications and navigation systems. Meanwhile, the beauty of the auroras is a direct result of these particles colliding with molecules in Earth's atmosphere, a serene reminder of our Sun's influence.\r\n\r\n[Scene 4: Observing and Predicting Solar Flares]\r\nThanks to advancements in technology, astronomers can now observe solar flares in unprecedented detail. Satellites like the Solar Dynamics Observatory (SDO) provide real-time data on the Sun's activity, helping scientists predict when and where solar flares might occur.\r\n\r\n[Cut to visual: Footage from the Solar Dynamics Observatory, showing solar flares]\r\nThis information is crucial for mitigating the potential impacts on Earth's technological infrastructure, allowing us to prepare and protect our satellites and power systems from the disruptive effects of these solar phenomena.\r\n\r\n[Scene 5: Conclusion]\r\nSolar flares are a testament to the Sun's incredible power and its dynamic nature. As we continue to study these magnificent events, we not only gain insight into our own star but also the workings of stars throughout the universe.\r\n\r\n[Cut to visual: Pan out from the Sun into the star-filled night sky]\r\nUnderstanding solar flares is not just about safeguarding our technology; it's about deepening our connection to the cosmos, reminding us of the intricate dance between energy, matter, and life itself.\r\n\r\nThank you for joining us on this journey through the fiery heart of our solar system. Until next time, keep looking up, and marvel at the wonders of the universe that surrounds us.";

            var trainingResponse1 = "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">\r\n<voice name=\"en-US-JennyNeural\">\r\nWelcome to our exploration of solar flares, the Sun's most captivating phenomena. The Sun, a ball of hot plasma and the heart of our solar system, is not just a source of light and warmth; it's a dynamic and turbulent star, capable of releasing incredible amounts of energy in the form of solar flares. <break time=\"1500ms\"/>\r\n\r\nImagine watching the Sun's surface, witnessing an intense brightening, a burst of light that outshines the surrounding areas. This is the beginning of a solar flare, a spectacular display of the Sun's power. <break time=\"1000ms\"/>\r\n\r\nSolar flares are sudden eruptions of energy on the Sun's surface. They result from the tangling, crossing, or reorganizing of magnetic field lines near sunspots. The energy released can be equivalent to millions of 100-megaton hydrogen bombs exploding at the same time. <break time=\"1500ms\"/>\r\n\r\nAs these magnetic fields snap and realign, they release a massive amount of energy in the form of light, heat, and a stream of highly energetic particles. This process is what we observe as a solar flare. <break time=\"1000ms\"/>\r\n\r\nSolar flares can have profound effects on Earth. The intense light and energetic particles can disrupt satellite operations, communication systems, and even power grids. They also contribute to the awe-inspiring natural light show known as the auroras, more commonly known as the Northern and Southern Lights. <break time=\"1500ms\"/>\r\n\r\nThe charged particles can ionize Earth's atmosphere, interfering with radio communications and navigation systems. Meanwhile, the beauty of the auroras is a direct result of these particles colliding with molecules in Earth's atmosphere, a serene reminder of our Sun's influence. <break time=\"1000ms\"/>\r\n\r\nThanks to advancements in technology, astronomers can now observe solar flares in unprecedented detail. Satellites like the Solar Dynamics Observatory (SDO) provide real-time data on the Sun's activity, helping scientists predict when and where solar flares might occur. <break time=\"1500ms\"/>\r\n\r\nThis information is crucial for mitigating the potential impacts on Earth's technological infrastructure, allowing us to prepare and protect our satellites and power systems from the disruptive effects of these solar phenomena. <break time=\"1000ms\"/>\r\n\r\nSolar flares are a testament to the Sun's incredible power and its dynamic nature. As we continue to study these magnificent events, we not only gain insight into our own star but also the workings of stars throughout the universe. <break time=\"1500ms\"/>\r\n\r\nUnderstanding solar flares is not just about safeguarding our technology; it's about deepening our connection to the cosmos, reminding us of the intricate dance between energy, matter, and life itself. <break time=\"1000ms\"/>\r\n\r\nThank you for joining us on this journey through the fiery heart of our solar system. Until next time, keep looking up, and marvel at the wonders of the universe that surrounds us.\r\n</voice>\r\n</speak>\r\n";

            var trainingScript2 = "Welcome to our cosmic journey through the Solar System, an awe-inspiring cluster of planets, moons, asteroids, and comets orbiting our star, the Sun. This celestial neighborhood is a complex and dynamic place, filled with wonders beyond our imagination.\r\n\r\n[Cut to visual: Panoramic view of the Solar System from space]\r\nFrom the fiery surface of the Sun to the icy realms of the outer planets, the Solar System is a testament to the diversity and beauty of the cosmos. Let's embark on a voyage to explore the major components that make up our Solar System.\r\n\r\n[Cut to visual: The Sun, radiating energy]\r\nAt the heart of the Solar System lies the Sun, a massive star that provides the necessary light and warmth to sustain life on Earth. It's a colossal sphere of hot plasma, dominating the Solar System by mass and gravity, guiding the orbits of all celestial bodies around it.\r\n\r\n[Cut to visual: Mercury, slowly rotating]\r\nOur first stop is Mercury, the closest planet to the Sun. Mercury is a rocky world, characterized by its heavily cratered surface and extreme temperature variations. Its proximity to the Sun makes it a challenging place to study, but it holds clues to understanding planetary formation.\r\n\r\n[Cut to visual: Venus, shrouded in thick clouds]\r\nNext is Venus, Earth's sister planet, enveloped in thick, toxic clouds that trap heat, making it the hottest planet in our Solar System. Despite its hostile conditions, Venus's volcanic landscape and mysterious atmosphere intrigue scientists.\r\n\r\n[Cut to visual: Earth, the blue marble]\r\nWe then reach Earth, the only known planet to support life. Our home is a vibrant world of oceans, continents, and a dynamic atmosphere, fostering diverse ecosystems. Earth's delicate balance makes it a unique gem in the vastness of space.\r\n\r\n[Cut to visual: Mars, the red planet]\r\nMars, the Red Planet, is our next destination. With its towering volcanoes, deep canyons, and evidence of ancient rivers, Mars is the focus of ongoing exploration to uncover its past and potential for life.\r\n\r\n[Cut to visual: Asteroid Belt, with various rocky bodies]\r\nBeyond Mars lies the Asteroid Belt, a region filled with rocky remnants from the early Solar System. These celestial bodies vary in size and composition, offering insights into the building blocks of planets.\r\n\r\n[Cut to visual: Jupiter, swirling with storms]\r\nJupiter, the gas giant, reigns as the largest planet in our Solar System. Its Great Red Spot, a giant storm larger than Earth, and its numerous moons, including the icy Europa and volcanic Io, make Jupiter a fascinating subject of study.\r\n\r\n[Cut to visual: Saturn, encircled by its rings]\r\nSaturn, known for its stunning rings, is the jewel of the Solar System. These rings, made of ice and rock, encircle the gas giant, while its moons hide potential for life beneath their icy crusts.\r\n\r\n[Cut to visual: Uranus, tilted on its side]\r\nUranus, the ice giant, stands out with its unique rotation, tilted on its side. Its icy atmosphere and ring system contribute to its cold, blue appearance.\r\n\r\n[Cut to visual: Neptune, blue and windy]\r\nFinally, Neptune, the furthest known planet from the Sun, is a world of high winds and deep blue oceans. It's a reminder of the Solar System's vastness and the mysteries that lie in the outer reaches.\r\n\r\n[Cut to visual: Comets and the Kuiper Belt, with Pluto in the distance]\r\nBeyond Neptune, the Kuiper Belt and scattered disc region host a variety of icy bodies, including Pluto. This region is the frontier of our Solar System, home to comets that journey towards the Sun, displaying their spectacular tails.\r\n\r\nThank you for joining us on this voyage through the Solar System. From the searing heat of the Sun to the icy realms of the outer planets, our cosmic neighborhood is a place of incredible diversity and beauty. As we continue to explore, we uncover more about our place in the universe, reminding us of the wonders that await in the vast expanse of space.";

            var trainingResponse2 = "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">\r\n    <voice name=\"en-US-JennyNeural\">\r\n        Welcome to our cosmic journey through the Solar System, an awe-inspiring cluster of planets, moons, asteroids, and comets orbiting our star, the Sun. This celestial neighborhood is a complex and dynamic place, filled with wonders beyond our imagination. <break time=\"2000ms\"/> <bookmark mark='gesture.wave-left-1'/>\r\n        \r\n        From the fiery surface of the Sun to the icy realms of the outer planets, the Solar System is a testament to the diversity and beauty of the cosmos. Let's embark on a voyage to explore the major components that make up our Solar System. <break time=\"2000ms\"/>\r\n        \r\n        At the heart of the Solar System lies the Sun, a massive star that provides the necessary light and warmth to sustain life on Earth. It's a colossal sphere of hot plasma, dominating the Solar System by mass and gravity, guiding the orbits of all celestial bodies around it. <break time=\"2000ms\"/>\r\n        \r\n        Our first stop is Mercury, the closest planet to the Sun. Mercury is a rocky world, characterized by its heavily cratered surface and extreme temperature variations. Its proximity to the Sun makes it a challenging place to study, but it holds clues to understanding planetary formation. <break time=\"2000ms\"/>\r\n        \r\n        Next is Venus, Earth's sister planet, enveloped in thick, toxic clouds that trap heat, making it the hottest planet in our Solar System. Despite its hostile conditions, Venus's volcanic landscape and mysterious atmosphere intrigue scientists. <break time=\"2000ms\"/>\r\n        \r\n        We then reach Earth, the only known planet to support life. Our home is a vibrant world of oceans, continents, and a dynamic atmosphere, fostering diverse ecosystems. Earth's delicate balance makes it a unique gem in the vastness of space. <break time=\"2000ms\"/> <bookmark mark='gesture.wave-right-1'/>\r\n        \r\n        Mars, the Red Planet, is our next destination. With its towering volcanoes, deep canyons, and evidence of ancient rivers, Mars is the focus of ongoing exploration to uncover its past and potential for life. <break time=\"2000ms\"/>\r\n        \r\n        Beyond Mars lies the Asteroid Belt, a region filled with rocky remnants from the early Solar System. These celestial bodies vary in size and composition, offering insights into the building blocks of planets. <break time=\"2000ms\"/>\r\n        \r\n        Jupiter, the gas giant, reigns as the largest planet in our Solar System. Its Great Red Spot, a giant storm larger than Earth, and its numerous moons, including the icy Europa and volcanic Io, make Jupiter a fascinating subject of study. <break time=\"2000ms\"/>\r\n        \r\n        Saturn, known for its stunning rings, is the jewel of the Solar System. These rings, made of ice and rock, encircle the gas giant, while its moons hide potential for life beneath their icy crusts. <break time=\"2000ms\"/>\r\n        \r\n        Uranus, the ice giant, stands out with its unique rotation, tilted on its side. Its icy atmosphere and ring system contribute to its cold, blue appearance. <break time=\"2000ms\"/>\r\n        \r\n        Finally, Neptune, the furthest known planet from the Sun, is a world of high winds and deep blue oceans. It's a reminder of the Solar System's vastness and the mysteries that lie in the outer reaches. <break time=\"2000ms\"/> <bookmark mark='gesture.wave-right-2'/>\r\n        \r\n        Beyond Neptune, the Kuiper Belt and scattered disc region host a variety of icy bodies, including Pluto. This region is the frontier of our Solar System, home to comets that journey towards the Sun, displaying their spectacular tails. <break time=\"2000ms\"/>\r\n        \r\n        Thank you for joining us on this voyage through the Solar System. From the searing heat of the Sun to the icy realms of the outer planets, our cosmic neighborhood is a place of incredible diversity and beauty. As we continue to explore, we uncover more about our place in the universe, reminding us of the wonders that await in the vast expanse of space. <break time=\"2000ms\"/> <bookmark mark='gesture.thumbsup'/>\r\n    </voice>\r\n</speak>\r\n";

            OpenAIRequest aiRequest = new OpenAIRequest
            {
                temperature = 0.4,
                top_p = 0.4,
                frequency_penalty = 0.3,
                presence_penalty = 0.3,
                max_tokens = 4096,
                stop = null,
                stream = false,
                messages = new List<Message>
                    {
                        new Message
                        {
                            role = "system",
                            content = "Your job is to take a video narration script and create a SSML out of it which will be used to create avatar based videos using azure speech. Make sure to double check that the SSML is a valid SSML with no extra tags and can be directly used in a speech service"
                        },
                        new Message
                        {
                            role = "user",
                            content = "Please create a SSML for the following script: " + trainingScript1
                        },
                        new Message
                        {
                            role = "assistant",
                            content = $"{trainingResponse1}"
                        },
                        new Message
                        {
                            role = "user",
                            content = "Please create a SSML for the following script: " + trainingScript2
                        },
                        new Message
                        {
                            role = "assistant",
                            content = $"{trainingResponse2}"
                        }
                    }
            };

            string language = lang == "EN" ? "english" : "hindi";

            aiRequest.messages.Add(new Message
            {
                role = "user",
                content = $"Please create a SSML for the following script: {script}"
            });

            return JsonConvert.SerializeObject(aiRequest);
        }

        private static string CreateVideoScript(string topic, string lang = "EN")
        {
            var trainingScript1 = "Write a script for the solar system";

            var trainingResponse1 = "Join us on a visual odyssey through the Solar System, a majestic ensemble of planets, moons, and celestial phenomena orbiting our life-giving star, the Sun. This journey will illuminate the beauty and diversity of these celestial bodies through the lens of our imaginations, guided by the power of visual imagery.\r\n\r\n[Cut to visual: \"The Sun radiating intense light and energy\"]\r\nOur voyage begins with the Sun, the heart of the Solar System, a blazing sphere of hydrogen and helium, its surface erupting with solar flares and prominences, casting a warm, life-sustaining glow on the planets that dance around it.\r\n\r\n[Cut to visual: \"Mercury's cratered surface against a backdrop of the Sun\"]\r\nAs we move closer, we encounter Mercury, a small, rocky planet. Its cratered surface tells stories of ancient cosmic impacts. Despite its proximity to the Sun, Mercury presents a stark, moon-like appearance, with shadows casting over its sun-scorched terrain.\r\n\r\n[Cut to visual: \"Venus's thick, cloud-covered atmosphere with hints of volcanic landscapes below\"]\r\nVenus, shrouded in thick, toxic clouds, reveals a world of extreme heat and volcanic mountains. Its mysterious clouds reflect a yellowish hue, hinting at the inferno that lies beneath, a greenhouse effect run amok.\r\n\r\n[Cut to visual: \"Earth from space, showcasing continents, oceans, and clouds\"]\r\nEarth, the blue marble, emerges in splendid color, its surface a harmony of blue oceans, green forests, and sandy deserts, wrapped in swirling white clouds. The delicate blue atmosphere hugs the planet, a reminder of our fragile oasis in the vastness of space.\r\n\r\n[Cut to visual: \"Mars's red, dusty landscape with Olympus Mons and Valles Marineris\"]\r\nMars, the Red Planet, stands out with its dusty, iron oxide-coated surface, home to the solar system's tallest volcano and the grand canyon, Valles Marineris. Its polar ice caps gleam in the sunlight, a stark contrast to the arid, reddish terrain.\r\n\r\n[Cut to visual: \"Asteroid Belt, a chaotic field of rocky debris orbiting the Sun\"]\r\nJourneying further, we navigate through the Asteroid Belt, a tumultuous zone of rocky debris. Each asteroid is a relic of the early Solar System, their irregular shapes and sizes tumbling through the void.\r\n\r\n[Cut to visual: \"Jupiter's swirling storms and the Great Red Spot, with Europa in the distance\"]\r\nNext is Jupiter, a gas giant adorned with swirling clouds and the iconic Great Red Spot, a storm larger than Earth. Its moons, like Europa, appear as distant, icy orbs, promising worlds of mystery.\r\n\r\n[Cut to visual: \"Saturn and its luminous rings, Enceladus casting water jets\"]\r\nSaturn dazzles with its magnificent ring system, a spectacle of ice and rock. Enceladus, one of its moons, jets water into space, hinting at subsurface oceans capable of harboring life.\r\n\r\n[Cut to visual: \"Uranus tilted on its axis, surrounded by faint rings\"]\r\nUranus reveals itself tilted, its greenish-blue hue attributed to methane in its atmosphere. This ice giant's faint rings circle it subtly, a testament to the diversity of planetary systems.\r\n\r\n[Cut to visual: \"Neptune's deep blue atmosphere, with a dark storm swirling\"]\r\nFinally, Neptune, in deep blue, stands as a sentinel at the edge of the known Solar System. Its dynamic atmosphere hosts dark storms, winds tearing across the planet at unimaginable speeds.\r\n\r\n[Cut to visual: \"Pluto and its heart-shaped glacier, with Charon in the background\"]\r\nOur odyssey concludes with Pluto, a dwarf planet with a heart-shaped glacier, a testament to the Solar System's diversity and beauty. Charon, its largest moon, shares a mutual dance in the darkness of space, on the frontier of our celestial neighborhood.\r\n\r\nThank you for embarking on this visual odyssey through the Solar System with us. Through the power of imagery, we've glimpsed the majesty and diversity of our cosmic surroundings, a reminder of the endless wonders waiting to be discovered in the vast expanse of the universe.";

            var trainingScript2 = "Write a script for the Roman Empire";

            var trainingResponse2 = "[Cut to visual: \"The founding of Rome, Romulus and Remus with the she-wolf\"]\r\nOur story begins with the legendary founding of Rome in 753 BC, where the twins Romulus and Remus are nurtured by a she-wolf. This mythic scene sets the stage for the rise of a city destined to become the heart of an empire.\r\n\r\n[Cut to visual: \"The Roman Republic, with the Senate and Roman Forum bustling with activity\"]\r\nThe Roman Republic lays the groundwork for empire, its Senate and the bustling Roman Forum at the center of political life. Visualize the Forum's grand temples and basilicas, the hub of Roman civic and religious life.\r\n\r\n[Cut to visual: \"Julius Caesar crossing the Rubicon, signaling the end of the Republic\"]\r\nJulius Caesar's bold crossing of the Rubicon River in 49 BC marks the end of the Republic. This pivotal moment, a declaration of war against the Senate, signifies the transition to imperial rule.\r\n\r\n[Cut to visual: \"Augustus standing victorious, heralding the Pax Romana\"]\r\nAugustus, Rome's first emperor, stands victorious, ushering in the Pax Romana, a period of unprecedented peace and prosperity. Under his reign, Rome expands its borders, and monumental buildings rise, symbolizing the empire's glory.\r\n\r\n[Cut to visual: \"The Colosseum, filled with spectators\"]\r\nThe Colosseum, Rome's grand amphitheater, echoes with the cheers of spectators. Gladiatorial combat and spectacular events entertain the masses, a testament to Roman engineering and social order.\r\n\r\n[Cut to visual: \"Panoramic view of the Roman Empire at its height, showcasing its vast territories\"]\r\nAt its height, the Roman Empire encompasses vast territories, from the deserts of Egypt to the forests of Germany. A panoramic view reveals a superpower controlling the Mediterranean, its roads connecting distant lands to the heart of Rome.\r\n\r\n[Cut to visual: \"Daily life in a bustling Roman market\"]\r\nImagine the daily life in a Roman market, a melting pot of cultures and goods. Citizens and slaves alike navigate stalls selling everything from exotic spices to handcrafted wares, illustrating the empire's economic vitality.\r\n\r\n[Cut to visual: \"The construction of aqueducts, bringing water to Rome's cities\"]\r\nRoman engineering prowess is on full display with the construction of aqueducts. These architectural marvels channel water to cities and towns, supporting public baths, fountains, and the sanitation systems that underpin urban life.\r\n\r\n[Cut to visual: \"The fall of Rome, with barbarian forces breaching its walls\"]\r\nThe empire's decline is marked by the fall of Rome, barbarian forces breaching its once invincible walls. The visual of Rome's sacking in 476 AD signifies the end of an era, the crumbling of a civilization that shaped the Western world.\r\n\r\n[Cut to visual: \"The legacy of Rome, in ruins and cultural imprints across Europe\"]\r\nOur journey concludes with Rome's enduring legacy, seen in ruins that dot the landscape and in the cultural, legal, and architectural imprints left across Europe. The remnants of the Roman Empire whisper stories of a past that continues to influence the present.\r\n\r\nThank you for joining us on this visual journey through the Roman Empire. From its mythical origins to its monumental legacy, the Roman Empire's story is a testament to the resilience and ingenuity of humanity, a saga etched in stone and memory across the ages.";

            var trainingScript3 = "Write a script for \r\nAzure Managed Identity simplifies authentication for Azure services by eliminating the need for explicit credentials, integrating with Azure Active Directory (AD) to provide a unique identity for each Azure resource. This system involves three main components: Azure Resources that need secure access, Managed Identities created for each resource for authentication without explicit credentials, and Azure AD, which acts as the identity provider.\r\n\r\nTo implement Managed Identity, services such as Azure Virtual Machines, Azure App Services, and Azure Functions enable a managed identity feature that allows secure access to other Azure resources without storing credentials. Access control is managed through Azure Role-Based Access Control (RBAC) and policies, with application code using Azure SDKs to access resources securely.\r\n\r\nFor SQL database access, Managed Identity replaces traditional connection strings with Azure AD tokens for authentication, removing the need to manage sensitive information like usernames and passwords. This is achieved through code that retrieves an Azure AD token for the SQL connection, simplifying database access management.\r\n\r\nHowever, there are challenges in implementing Managed Identity, including resource compatibility, integration with legacy systems, understanding roles and permissions, and monitoring and auditing for compliance and security.\r\n\r\nThe benefits of using Azure Managed Identity include enhanced security by removing the need to manage credentials, simplified management and operational overhead, scalability, compliance with regulatory requirements, and increased developer productivity by focusing on code rather than authentication mechanisms.\r\n\r\nIn conclusion, Azure Managed Identity offers a secure and efficient way to manage identity and access in the cloud, supporting modern cloud architectures while addressing potential implementation challenges with its benefits in security, management, scalability, and compliance.\r\n\r\n\r\n\r\n\r\n";

            var trainingResponse3 = "[Cut to visual: A digital cloud with a shield symbolizing security]\r\n\"In today's cloud-driven world, managing security and identity access can be a complex challenge. But what if there was a way to simplify it all?\"\r\n[Cut to visual: Animated Azure logo transforming into a digital key]\r\n\"Enter Azure Managed Identity, a game-changing solution by Microsoft that eliminates the need for explicit credentials, making application authentication seamless and secure.\"\r\n[Cut to visual: Three interconnected icons - a cloud (representing Azure Resources), a badge (for Managed Identity), and a lock (for Azure AD)]\r\n\"The magic happens in three parts: Azure Resources needing secure access, Managed Identities that authenticate without credentials, and Azure AD, which verifies and allows these interactions.\"\r\n[Cut to visual: A checklist with icons - a virtual machine, a web service, and a function symbol]\r\n\"Setting it up is straightforward. Enable Managed Identity on your Azure services, like Virtual Machines, App Services, and Functions, for secure, credential-free access to other Azure resources.\"\r\n[Cut to visual: A safe with a database icon, opening with a digital key]\r\n\"Imagine connecting to an Azure SQL Database without traditional credentials. With Managed Identity, a simple token from Azure AD is all you need for authentication.\"\r\n[Cut to visual: A hurdle track with some hurdles labeled \"Resource Compatibility,\" \"Legacy Systems,\" and \"Roles Understanding\"]\r\n\"Of course, some hurdles like resource compatibility and integration with legacy systems may arise. But the benefits, including enhanced security and simplified management, make it worth the effort.\"\r\n[Cut to visual: A shield breaking chains labeled \"credentials\" and \"complexity\"]\r\n\"The payoff? Improved security, reduced operational overhead, scalability, compliance, and letting developers focus on what they do best – coding.\"\r\n[Cut to visual: A digital cloud with a checkmark and various Azure services orbiting around it]\r\n\"Azure Managed Identity not only secures cloud identity management and access control but also paves the way for a more streamlined, secure, and efficient cloud environment.\"\r\n[Cut to visual: Earth from space with digital connections enveloping the globe]\r\n\"Embrace the future of cloud security with Azure Managed Identity. Simplify, secure, and scale your cloud architecture effortlessly.\"";

            OpenAIRequest aiRequest = new OpenAIRequest
            {
                temperature = 0.5,
                top_p = 0.5,
                frequency_penalty = 0.3,
                presence_penalty = 0.3,
                max_tokens = 4096,
                stop = null,
                stream = false,
                messages = new List<Message>
                    {
                        new Message
                        {
                            role = "system",
                            content = "Your job is to create a video narration script for the narrator of a short content on some topic or a summary of some topic. Add the [Cut to visual: (Visual description)] tag for queues to visual photographs. These tags will be used to generate images using DALLE model so make sure the visual description is in a format DALLE understands"
                        },
                        new Message
                        {
                            role = "user",
                            content = trainingScript1
                        },
                        new Message
                        {
                            role = "assistant",
                            content = $"{trainingResponse1}"
                        },
                        new Message
                        {
                            role = "user",
                            content = trainingScript2
                        },
                        new Message
                        {
                            role = "assistant",
                            content = $"{trainingResponse2}"
                        },
                        new Message
                        {
                            role = "user",
                            content = $"{trainingScript3}"
                        },
                        new Message
                        {
                            role = "assistant",
                            content = $"{trainingResponse3}"
                        }
                    }
            };

            string language = lang == "EN" ? "english" : "hindi";

            aiRequest.messages.Add(new Message
            {
                role = "user",
                content = $"Write a script for {topic}"
            });

            return JsonConvert.SerializeObject(aiRequest);
        }
    }
}
