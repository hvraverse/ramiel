package discord

import (
	"fmt"
	"log"

	"github.com/bwmarrin/discordgo"
)

type DiscordConfig struct {
	BotToken       string
	GuildID        string
	RemoveCommands bool
}

type DiscordClient struct {
	config  *DiscordConfig
	session *discordgo.Session
}

func New(discordConfig *DiscordConfig) *DiscordClient {
	return &DiscordClient{
		config: discordConfig,
	}
}

func (d *DiscordClient) Connect() error {
	var err error

	d.session, err = discordgo.New("Bot " + d.config.BotToken)
	if err != nil {
		return fmt.Errorf("Invalid bot parameters: %v", err)
	}

	d.session.AddHandler(func(s *discordgo.Session, r *discordgo.Ready) {
		log.Println("Discord connected...")

		err = d.loadCommands()
		if err != nil {
			log.Println(err)
		}
	})

	err = d.session.Open()
	if err != nil {
		return fmt.Errorf("Cannot open the session: %v", err)
	}

	return nil
}

func (d *DiscordClient) Close() error {
	var err error
	if d.config.RemoveCommands {
		log.Println("Removing Commands...")
		//err = d.removeCommands()
	}

	if d.session != nil {
		log.Println("Closing Session...")
		sError := d.session.Close()
		if sError != nil {
			return sError
		}
	}

	return err
}
